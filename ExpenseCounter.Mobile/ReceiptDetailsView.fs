module ReceiptDetailsView

  open Fabulous.XamarinForms
  open Xamarin.Forms
  open Receipts
  open Model
  open Update
  open System
  open System.Globalization

  let rec private exnDisplay padding header (e: exn) =
    View.StackLayout(padding = Thickness(padding, 0., 0., 0.), children = [
      View.Label(text = header)
      View.Editor(
        text = e.Message,
        isReadOnly = true,
        fontSize = Named NamedSize.Small
      )
      View.ScrollView(
        height = 120.,
        backgroundColor = Color.FromHex("EEEEEE"),
        content = 
          View.Editor(
            text = e.StackTrace,
            isReadOnly = true,
            fontSize = Named NamedSize.Micro
          )
      )
      match e with
      | :? AggregateException as aggr ->
        for inner in aggr.InnerExceptions do
          exnDisplay 10. "Inner exception" inner
      | e when e.InnerException <> null -> exnDisplay 10. "Inner exception" e.InnerException
      | _ -> View.Label(height = 0., width = 0.)
    ])

  let private scannedDataDisplay str =
    View.StackLayout(children = [
      View.Label(text = "Scanned data")
      View.Entry(text = str, isReadOnly = true)
    ])

  let private parsedReceiptDisplay parsed =
    View.StackLayout(children = [
      View.Label(text = sprintf "Receipt printed at: %A" parsed.Time)
      View.Label(text = sprintf "Sum: %s" (parsed.Sum.ToString("c", CultureInfo.GetCultureInfo("ru-RU"))))
      View.Label(text = sprintf "Fiscal register number: %s" parsed.FiscalNumber)
      View.Label(text = sprintf "Document number: %s" parsed.FiscalDocumentNumber)
      View.Label(text = sprintf "Fiscal signature: %s" parsed.FiscalSignature)
    ])

  let private detailedReceiptDisplay detailed =
    View.StackLayout(children = [
      parsedReceiptDisplay detailed.Identifiers
      View.Label(text = sprintf "Seller TIN: %s" detailed.SellerTIN)
      View.Label(text = sprintf "Retailer address: %s" (Option.defaultValue "unknown" detailed.RetailAddress))
      View.Label(text = sprintf "Store name: %s" (Option.defaultValue "unknown" detailed.StoreName))
      View.Label(text = "Positions:")
      let ru_RU = CultureInfo.GetCultureInfo("ru-RU")
      for position in detailed.Positions do
        View.Label(text =
          sprintf "%sx%s (%s per one): %s"
            (position.Quantity.ToString("#####0.###", ru_RU))
            position.Name 
            (position.Price.ToString("c", ru_RU))
            (position.Sum.ToString("c", ru_RU))
       )
    ])

  let private display (receipt: ReceiptDTO) =
    let localTime = DateTime.SpecifyKind(receipt.LastAction, DateTimeKind.Utc).ToLocalTime()

    View.StackLayout(
      children = [
          
        View.Label(
          text = sprintf "Status: %s" (stageToString receipt),
          textColor = if Option.isSome receipt.Error then Color.Accent else Color.Default
        )
        View.Label(text = sprintf "Last update: %A" localTime)

        match receipt.Receipt with
        | RawScanned str -> scannedDataDisplay str
        | ParseFailed (str, _) -> scannedDataDisplay str
        | Parsed parsed -> parsedReceiptDisplay parsed
        | DetailsGettingFailed (parsed, _) -> parsedReceiptDisplay parsed
        | Detailed detailed -> detailedReceiptDisplay detailed
        | Uploaded detailed -> detailedReceiptDisplay detailed
        | UploadFailed (detailed, _) -> detailedReceiptDisplay detailed

        receipt.Error
        |> Option.map (exnDisplay 0. "Error:")
        |> Option.defaultWith (fun () -> View.Label(text = "No errors occured during processing"))
      ]
    )
    
  let view model dispatch =
    let close () = dispatch (Navigate Main)

    match model.CurrentView with
    | ReceiptDetails receipt ->
      let hasError = Option.isSome receipt.Error

      View.Grid(padding = Thickness 20.,
        rowdefs = [ Auto; Star; Auto ],
        coldefs = [ Auto ],
        children = [
        
          View.FlexLayout(direction = FlexDirection.Row, justifyContent = FlexJustify.SpaceBetween, height = 32.,
            children = [
              View.Label(
                text = sprintf "Receipt #%d" receipt.Id,
                fontSize = Named NamedSize.Title
              )
              View.ImageButton(
                source = ImageSrc (ImageSource.FromResource("ExpenseCounter.Mobile.icon_recycle.png", typeof<Msg>.Assembly)),
                width = 32.0,
                height = 32.0,
                aspect = Aspect.AspectFit,
                backgroundColor = Color.Transparent,
                command = (fun () ->
                  Async.Start (async { do Receipts.deleteReceipt receipt })
                  close()
                )
              )
            ]
          )
          |> row 0

          View.ScrollView(content = display receipt)
          |> row 1

          if hasError then
            View.StackLayout(orientation = StackOrientation.Horizontal,
              children = [
                View.Button(text = "Close", command = close)
                View.Button(text = "Retry", command = fun () -> dispatch (Retry receipt))
              ]
            )
            |> row 2
          else
            View.Button(text = "Close", command = close)
            |> row 2
        ]
      )
    | _ -> View.StackLayout()
