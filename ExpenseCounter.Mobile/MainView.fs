module MainView

  open Fabulous.XamarinForms
  open Xamarin.Forms
  open Model
  open Update
  open System
  open Receipts
  
  
  let receiptDisplay (receipt: ReceiptDTO) =
    let localTime = DateTime.SpecifyKind(receipt.LastAction, DateTimeKind.Utc).ToLocalTime()
    let state = stageToString receipt
  
    View.Label(
      text = sprintf "%A %s" localTime state,
      padding = Thickness 5.,
      textColor = if Option.isSome receipt.Error then Color.Accent else Color.Default
    )

  let view model dispatch =
    View.FlexLayout(padding = Thickness 20., direction = FlexDirection.Column,
      children = [
        View.FlexLayout(justifyContent = FlexJustify.SpaceBetween, height = 32.0,
          children = [
            View.Label(text = "Receipts", fontSize = FontSize.Named NamedSize.Title, horizontalOptions = LayoutOptions.Center)
            View.ImageButton(
              source = ImageSrc (ImageSource.FromResource("ExpenseCounter.Mobile.icon_settings.png", typeof<Msg>.Assembly)),
              command = (fun () -> dispatch (Navigate Settings)),
              width = 32.0,
              height = 32.0,
              aspect = Aspect.AspectFit,
              horizontalOptions = LayoutOptions.End,
              backgroundColor = Color.Transparent
            )
          ]
        )
        grow 1. <| View.FlexLayout(direction = FlexDirection.Column,
          children = [
            View.ScrollView(padding = Thickness(0., 20., 0., 0.), verticalOptions = LayoutOptions.Fill,
              content = View.StackLayout(
                children = [
                  if model.CurrentReceipts.Length = 0 then
                    yield View.Label("No receipts scanned yed...")
                  else
                    for receipt in model.CurrentReceipts ->
                      receiptDisplay receipt
                ]
              )
            )
          ]
        )
        alignSelf FlexAlignSelf.End <| View.ImageButton(
          source = ImageSrc (ImageSource.FromResource("ExpenseCounter.Mobile.icon_scan_qr.png", typeof<Msg>.Assembly)),
          command = (fun () -> dispatch (Navigate Scanner)),
          width = 32.0,
          height = 32.0,
          aspect = Aspect.AspectFit,
          horizontalOptions = LayoutOptions.End,
          verticalOptions = LayoutOptions.End,
          backgroundColor = Color.Transparent
        )
      ]
    )