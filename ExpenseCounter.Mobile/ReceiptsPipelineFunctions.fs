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
        match exn with
        | :? AggregateException as aggr -> ParseFailed (qs, AggregateException([| yield! aggr.InnerExceptions; yield e |]))
        | old -> ParseFailed (qs, AggregateException(old, e))
    | any -> any
