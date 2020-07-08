module ReceiptsPipelineFunctions 

  open Receipts
  open System
  open QueryStringValueExtractor
  open CheckReceiptSDK

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
