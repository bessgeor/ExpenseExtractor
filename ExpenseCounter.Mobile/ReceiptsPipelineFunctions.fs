module ReceiptsPipelineFunctions 

  open Receipts
  open System
  open QueryStringValueExtractor
  open CheckReceiptSDK
  open Microsoft.Graph
  open System.Threading.Tasks
  open Newtonsoft.Json.Linq

  let private parse qs =
    {
      Time = extractQueryStringComponent "t" dateTimeReviver qs
      Sum = extractQueryStringComponent "s" decimalReviver qs
      FiscalNumber = extractQueryStringComponent "fn" stringReviver qs
      FiscalDocumentNumber = extractQueryStringComponent "i" stringReviver qs
      FiscalSignature = extractQueryStringComponent "fp" stringReviver qs
    }

  let private handleAggrException (exn: Exception) (e: Exception) (v: 'value) (f: 'value*Exception -> 'result) =
    match exn with
    | :? AggregateException as aggr -> f (v, AggregateException([| yield! aggr.InnerExceptions; yield e |]))
    | old -> f (v, AggregateException(old, e))

  let tryParse receipt =
    match receipt with
    | RawScanned qs ->
      try
        let parsed = parse qs
        Parsed parsed
      with | :? Exception as e -> ParseFailed (qs, e)
    | ParseFailed (qs, exn) ->
      try
        let parsed = parse qs
        Parsed parsed
      with | :? Exception as e ->
        handleAggrException exn e qs ParseFailed
    | any -> any

  let private handleFNSResponse taskName receipt onSuccess (responseTask: Threading.Tasks.Task<#Results.Result>) =
    async {
      try
        let! response = Async.AwaitTask responseTask
        if not response.IsSuccess then
          return DetailsGettingFailed (receipt, Exception(sprintf "%s: %A (%s)" taskName response.StatusCode response.Message))
        else
          return! onSuccess response
      with | :? Exception as e -> return DetailsGettingFailed (receipt, e)
    }

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

  let private getDetails (receipt: ParsedReceipt) =
    async {
      let! credentialsOption = OfdCredentials.get()
      if ValueOption.isNone credentialsOption then
        return DetailsGettingFailed (receipt, Exception("No credentials stored"))
      else
          let credentials = ValueOption.get credentialsOption
          return!
            FNS.LoginAsync (credentials.Phone, credentials.Password)
            |> handleFNSResponse "login" receipt (fun _ ->
              FNS.CheckAsync (receipt.FiscalNumber, receipt.FiscalDocumentNumber, receipt.FiscalSignature, receipt.Time, receipt.Sum)
              |> handleFNSResponse "check receipt" receipt (fun response ->
                if not response.ReceiptExists then
                  async.Return (DetailsGettingFailed (receipt, Exception("Federal Tax Service claims that this receipt does not exist")))
                else
                  FNS.ReceiveAsync (receipt.FiscalNumber, receipt.FiscalDocumentNumber, receipt.FiscalSignature, credentials.Phone, credentials.Password)
                  |> handleFNSResponse "receive receipt details" receipt (fun resp -> parseDetailsFromResponse resp.Document.Receipt receipt |> Detailed |> async.Return)
              )
            )
    }

  let tryDetail receipt =
    async {
      match receipt with
      | Parsed r ->
        return! getDetails r
      | DetailsGettingFailed (r, exn) ->
        let! detailed = getDetails r
        match detailed with
        | DetailsGettingFailed (r, e) -> return handleAggrException exn e r DetailsGettingFailed
        | any -> return any
      | any -> return any
    }


  let private upload receipts =
    async {
      let! link = SharingLink.getEncodedSharingLink()
      if (ValueOption.isNone link) then
        raise (Exception("no excel file link specified"))
      let! driveItem = MSGraphAPI.requestSharedFile (ValueOption.get link)
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
                  box receipt.RetailAddress
                  box receipt.StoreName
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
      | :? Exception as e ->
        return
          onlyUploadable
          |> Array.map (fun (dto, detailed) -> { dto with Receipt = UploadFailed (detailed, e) })
    }