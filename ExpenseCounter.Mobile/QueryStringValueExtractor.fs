module QueryStringValueExtractor
  open System
  open System.Globalization
  open System.Collections.Generic

  let stringReviver (qs: string) start length =
    qs.Substring(start, length)

  let decimalReviver (qs: string) start length =
    let span = qs.AsSpan().Slice(start, length)
    Decimal.Parse(span, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)

  let dateTimeFormats =
    [|
      "yyyyMMddTHHmm"
      "yyyyMMddTHHmmss"
    |]

  let dateTimeReviver (qs: string) start length =
    let span = qs.AsSpan().Slice(start, length)
    DateTime.ParseExact(span, dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)

  let rec private findKeyIndex (qs: string) (qsComponent: string) startFrom =
    if qs.Length < qsComponent.Length + 1 then
      -1
    else if startFrom = 0 && qs.StartsWith(qsComponent) && qs.[ qsComponent.Length ] = '=' then
      qsComponent.Length
    else
      let ampIdx = qs.IndexOf('&', startFrom)
      if ampIdx < 0 then
        -1
      else
        let cmpIdx = qs.IndexOf(qsComponent, ampIdx)
        let eqIdx = ampIdx + qsComponent.Length + 1
        if cmpIdx < 0 then
          -1
        else if cmpIdx - ampIdx = 1 then
          if qs.Length > eqIdx then
            if qs.[ eqIdx ] = '=' then
              eqIdx
            else
              findKeyIndex qs qsComponent eqIdx
          else
            -1
        else
          findKeyIndex qs qsComponent (cmpIdx - 1)


  let extractQueryStringComponent (qsComponent: string) reviver (queryString: string) =
    let compIdx = findKeyIndex queryString qsComponent 0
    if compIdx < 0 then
      let error = sprintf "No key %s found in query string %s" qsComponent queryString
      raise (KeyNotFoundException(error))
    else
      let start = compIdx + 1
      let endIdxRaw = queryString.IndexOf('&', compIdx)
      let endIdx = if endIdxRaw < 0 then queryString.Length else endIdxRaw
      reviver queryString start (endIdx - start)

