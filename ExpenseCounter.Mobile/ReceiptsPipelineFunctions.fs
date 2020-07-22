module ReceiptsPipelineFunctions 

  open Receipts
  open System
  open QueryStringValueExtractor
  open CheckReceiptSDK
  open Microsoft.Graph
  open System.Threading.Tasks
  open Newtonsoft.Json.Linq
  open Xamarin.Essentials

  let private parse qs =
    {
      Time = extractQueryStringComponent "t" dateTimeReviver qs
      Sum = extractQueryStringComponent "s" decimalReviver qs
      FiscalNumber = extractQueryStringComponent "fn" stringReviver qs
      FiscalDocumentNumber = extractQueryStringComponent "i" stringReviver qs
      FiscalSignature = extractQueryStringComponent "fp" stringReviver qs
    }

  let private mergeErrors a b =
    match (a,b) with
    | Simple e1, Simple e2 -> Combined [ e1; e2 ]
    | Combined cmb, Simple s -> Combined [ yield! cmb; s ]
    | Simple s, Combined cmb -> Combined [ s; yield! cmb ]
    | Combined e1, Combined e2 -> Combined [ yield! e1; yield! e2 ]

  let rec private exnToErrorValue (e: exn) =
    {
      Message = e.Message;
      StackTrace = e.StackTrace;
      Inner = ValueOption.ofObj e.InnerException |> ValueOption.map exnToErrorValue
    }

  let private exnToError (e: exn) =
    match e with
    | :? AggregateException as aggr -> aggr.InnerExceptions |> Seq.map exnToErrorValue |> Seq.toList |> Combined
    | e -> e |> exnToErrorValue |> Simple

  let private stringToError s =
    Simple { Message = s; StackTrace = ""; Inner = ValueNone }

  let private handleAggrException (exn: ReceiptError) (e: exn) (v: 'value) (f: 'value*ReceiptError -> 'result) =
    e
    |> exnToError
    |> mergeErrors exn
    |> fun e -> f (v, e)

  let tryParse receipt =
    match receipt with
    | RawScanned qs ->
      try
        let parsed = parse qs
        Parsed parsed
      with | e -> ParseFailed (qs, exnToError e)
    | ParseFailed (qs, exn) ->
      try
        let parsed = parse qs
        Parsed parsed
      with | e ->
        handleAggrException exn e qs ParseFailed
    | any -> any

  let private parseDetailsFromResponse (doc: Results.Receipt) receipt =
    {
      Identifiers = receipt
      SellerTIN = doc.RetailInn
      RetailAddress = Option.ofObj doc.RetailPlaceAddress
      StoreName = Option.ofObj doc.StoreName
      IssuedAt = doc.ReceiptDateTime
      Positions = 
        doc.Items
        |> Seq.map (fun i -> {
          Name = i.Name
          Quantity = i.Quantity
          Price = (decimal i.Price) / 100M
          Sum = (decimal i.Sum) / 100M
        })
        |> Seq.toArray
    }

  let private expandAggregateException (e: exn) =
    match e with
    | :? AggregateException as aggr -> seq aggr.InnerExceptions
    | _ -> Seq.singleton e
  let private expandAggregateExceptions e1 e2 =
    seq {
      yield! expandAggregateException e1
      yield! expandAggregateException e2
    }
    
  type FederalTaxServiceResult<'T> =
    | Success of 'T
    | Failure of ReceiptError

  type FederalTaxService () =
    member _.Return v = async.Return (Success v)

    member _.Bind (m: Async<FederalTaxServiceResult<'v>>, binder: 'v -> Async<FederalTaxServiceResult<'u>>) =
      async {
        if (Connectivity.NetworkAccess <> NetworkAccess.Internet) then
          return Failure (stringToError "no stable Internet connection")
        else
          let! m = m
          match m with
          | Failure e -> return Failure e
          | Success v ->
              try
                return! binder v
              with
              | e -> return Failure (exnToError e)
      }

    static member Sleep time =
      async {
        do! Async.Sleep time
        return Success ()
      }

  let fts = FederalTaxService()

  let private ftsRequest name (req: Task<#Results.Result>) =
    async {
      let! v = req |> Async.AwaitTask
      if not v.IsSuccess then
        return Failure (stringToError <| sprintf "%s: %A (%s)" name v.StatusCode v.Message)
      else
        return Success v
    }

  let private loginAsync (credentials: OfdCredentials.OfdCredentials) =
    FNS.LoginAsync (credentials.Phone, credentials.Password)
    |> ftsRequest "login"

  let private existsAsync (receipt) = 
    FNS.CheckAsync(
      receipt.FiscalNumber,
      receipt.FiscalDocumentNumber,
      receipt.FiscalSignature,
      receipt.Time,
      receipt.Sum
    )
    |> ftsRequest "exists check"
    |> fun x -> async {
        let! v = x
        match v with
        | Failure _ -> return v
        | Success s ->
          if not s.ReceiptExists then
            return (Failure (stringToError "Federal Tax Service claims that this receipt does not exist"))
          else return Success s
      }

  let private receiveAsync (credentials: OfdCredentials.OfdCredentials) receipt =
    FNS.ReceiveAsync(
      receipt.FiscalNumber,
      receipt.FiscalDocumentNumber,
      receipt.FiscalSignature,
      credentials.Phone,
      credentials.Password
    )
    |> ftsRequest "details getting"

  let private req (credentials: OfdCredentials.OfdCredentials) receipt =
    fts {
      let! _ = loginAsync credentials
      let! _ = existsAsync receipt
      do! FederalTaxService.Sleep 1000
      let! _ = existsAsync receipt
      let! details = receiveAsync credentials receipt
      return parseDetailsFromResponse details.Document.Receipt receipt
    }

  let private requestFTSDetails (credentials: OfdCredentials.OfdCredentials) receipt =
    async {
      let! res = req credentials receipt
      return 
        match res with
        | Success v -> Detailed v
        | Failure e -> DetailsGettingFailed (receipt, e)
    }

  let private getDetails (receipt: ParsedReceipt) =
    async {
      let! credentialsOption = OfdCredentials.get()
      if ValueOption.isNone credentialsOption then
        return DetailsGettingFailed (receipt, stringToError "No credentials stored")
      else
        let credentials = ValueOption.get credentialsOption
        return! requestFTSDetails credentials receipt
    }

  let tryDetail receipt =
    async {
      match receipt with
      | Parsed r ->
        return! getDetails r
      | DetailsGettingFailed (r, exn) ->
        let! detailed = getDetails r
        match detailed with
        | DetailsGettingFailed (r, e) -> return DetailsGettingFailed (r, mergeErrors exn e)
        | any -> return any
      | any -> return any
    }

  let uploadInternetDepending encodedSharingLink receipts =
    async {
      let! driveItem = MSGraphAPI.requestSharedFile encodedSharingLink
      let groupedReceipts = receipts |> Array.groupBy (snd >> fun x -> x.IssuedAt.Year, x.IssuedAt.Month)

      for (year, month), receipts in groupedReceipts do
        let worksheetName = sprintf "%d.%s" year (month.ToString().PadLeft(2, '0'))
        let worksheetApi = MSGraphAPI.client.Me.Drive.Items.Item(driveItem.Id).Workbook.Worksheets.Item(worksheetName)
        let! worksheet = worksheetApi.Request().GetAsync() |> Async.AwaitTask
        let error = MSGraphAPI.getError worksheet
        let task =
          match error with
          | ValueSome err ->
            match err with
            | :? JObject as jo when jo.Value("code") = "ItemNotFound" ->
              worksheetApi.Request().CreateAsync(WorkbookWorksheet(Name = worksheetName, Position = Nullable 0, Visibility = "Visible"))
            | e -> Task.FromException<WorkbookWorksheet>(Exception(e.ToString()))
          | ValueNone -> Task.FromResult worksheet
        let! worksheet = task |> Async.AwaitTask
        do MSGraphAPI.throwOnError worksheet

        let! usedRange = worksheetApi.UsedRange(true).Request().GetAsync() |> Async.AwaitTask
        do MSGraphAPI.throwOnError usedRange

        let receiptData =
          receipts
          |> Seq.map snd
          |> Seq.distinctBy (fun x -> x.Identifiers)
          |> Seq.toArray

        let rowCount = (usedRange.Text :?> JArray).Count
        let startRowNum = if rowCount = 0 then 1 else rowCount + 1
        let newRowsCount = receiptData |> Seq.sumBy (fun x -> x.Positions.Length)
        let finishRowNum = startRowNum + (newRowsCount - 1)
        let range = sprintf "A%d:Q%d" startRowNum finishRowNum

        let positions = 
          [|
            for receipt in receiptData do
              for position in receipt.Positions do
                JArray([|
                  receipt.IssuedAt.ToOADate() |> box
                  null
                  box position.Quantity
                  box position.Price
                  box position.Sum
                  box position.Name
                  null; null; null; null; null; null // separate out other data which may be useful for ML but not for user
                  box receipt.SellerTIN
                  box <| Option.toObj receipt.RetailAddress
                  box <| Option.toObj receipt.StoreName
                  box receipt.Identifiers.FiscalNumber
                  box receipt.Identifiers.FiscalDocumentNumber
                |])
          |]
        let newValues = JArray(positions)

        let wbRange = WorkbookRange(Values = newValues)
        let! response = worksheetApi.Range(range).Request().PatchAsync(wbRange) |> Async.AwaitTask
        do MSGraphAPI.throwOnError response
      return receipts |> Array.map (fun (dto, detailed) -> { dto with Receipt = Uploaded detailed })
    }


  let private upload receipts =
    async {
      let! link = SharingLink.getEncodedSharingLink()
      if (ValueOption.isNone link) then
        raise (Exception("no excel file link specified"))
      let encodedLink = ValueOption.get link
      if Connectivity.NetworkAccess <> NetworkAccess.Internet then
        raise (Exception "no stable Internet connection")
      return! uploadInternetDepending encodedLink receipts
    }

  let tryUpload receipts =
    async {
      let onlyUploadable =
        receipts
        |> Seq.map (
          fun x -> x.Receipt |> function
          | Detailed d -> ValueSome (x,d)
          | UploadFailed (d, _) -> ValueSome (x,d)
          | _ -> ValueNone
        )
        |> Seq.filter ValueOption.isSome
        |> Seq.map ValueOption.get
        |> Seq.toArray
      try
        let! uploaded = upload onlyUploadable
        return uploaded
      with
      | e ->
        return
          onlyUploadable
          |> Array.map (fun (dto, detailed) -> { dto with Receipt = UploadFailed (detailed, exnToError e) })
    }