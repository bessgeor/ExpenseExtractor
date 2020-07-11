// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace ExpenseCounter.Mobile

open System.Diagnostics
open System.Text.RegularExpressions
open Fabulous
open Fabulous.XamarinForms
open Fabulous.XamarinForms.LiveUpdate
open Xamarin.Forms
open System
open Receipts
open System.Threading

module App =
  type Views =
    | Main
    | Settings
    | Scanner

  type Model =
    {
      CurrentView: Views
      Phone: string
      Password: string
      SharingLink: string
      SettingsSaveError: string voption

      CurrentReceipts: ReceiptDTO array
    }

  type Msg =
    | Navigate of Views
    | LoadedSettingsFromStorage of OfdCredentials.OfdCredentials voption * string voption
    | PhoneChanged of string
    | PasswordChanged of string
    | SharingLinkChanged of string
    | SaveSettings
    | SettingsSaveError of string
    | SettingsSaved

    | ReceiptsUpdated of ReceiptDTO array
    | RequireMSALSignIn
    | MSALSignedIn

    | Scanned of string

  let initModel =
    {
      CurrentView = Main
      Phone = "+7"
      Password = ""
      SharingLink = ""
      SettingsSaveError = ValueNone

      CurrentReceipts = [||]
    }

  let updateReceipts () =
    Receipts.getLatestReceipts 10
    |> ReceiptsUpdated

  let init () = initModel, Cmd.ofMsg (updateReceipts())

  let private loadCredentialsFromStorage =
    async {
      let! credentials = OfdCredentials.get()
      let! sharingLink = SharingLink.getUnencodedSharingLink()
      return LoadedSettingsFromStorage (credentials, sharingLink)
    }
    |> Cmd.ofAsyncMsg

  let private saveCredentialsToStorage model =
    async {
      do! OfdCredentials.set { Phone = model.Phone; Password = model.Password }
      let encodedLink = SharingLink.encodeSharingLink model.SharingLink
      try
        let! document = MSGraphAPI.requestSharedFile encodedLink
        do MSGraphAPI.throwOnError document
        if document.File.MimeType <> "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" then
          return SettingsSaveError "File is not Excel xslx spreadsheet"
        else
          do! SharingLink.setUnencodedSharingLink model.SharingLink
          return SettingsSaved
      with
      | :? Exception as e -> return SettingsSaveError e.Message
    }
    |> Cmd.ofAsyncMsg

  let private msalSignIn =
    async {
      do! MSALSignIn.signIn()
      return MSALSignedIn
    }
    |> Cmd.ofAsyncMsg

  let update msg model =
    match msg with
    | Navigate view -> { model with CurrentView = view }, loadCredentialsFromStorage
    | LoadedSettingsFromStorage (cred, link) ->
      let model =
        cred
        |> ValueOption.map (fun creds -> { model with Phone = creds.Phone; Password = creds.Password })
        |> ValueOption.defaultValue model
      link
      |> ValueOption.map (fun link -> { model with SharingLink = link })
      |> ValueOption.defaultValue model
      , Cmd.none
    | PhoneChanged phone -> { model with Phone = phone }, Cmd.none
    | PasswordChanged pwd -> { model with Password = pwd }, Cmd.none
    | SharingLinkChanged link -> { model with SharingLink = link }, Cmd.none
    | SaveSettings -> model, saveCredentialsToStorage model
    | SettingsSaveError err -> { model with SettingsSaveError = ValueSome err }, Cmd.none
    | SettingsSaved -> { model with Phone = initModel.Phone; Password = initModel.Password; SharingLink = initModel.SharingLink }, Cmd.ofMsg <| Navigate Main

    | ReceiptsUpdated receipts -> { model with CurrentReceipts = receipts }, Cmd.none
    | RequireMSALSignIn -> model, msalSignIn
    | MSALSignedIn -> model, Cmd.none
    
    | Scanned text -> model, Cmd.ofAsyncMsg <| async {
      do Async.Start <| async { do Receipts.addReceiptFromScan text }
      return Navigate Main
    }

  let phoneValid = Regex(@"^\+7\d{3}\d{7}$")
  let paswdValid = Regex(@"^\d{6}$")

  let receiptDisplay (receipt: ReceiptDTO) =
    let localTime = DateTime.SpecifyKind(receipt.LastAction, DateTimeKind.Utc).ToLocalTime()
    let state =
      match receipt.Stage with
      | Scan -> "scanned receipt"
      | Parse -> "parsed receipt"
      | ParsingError -> "receipt parse failed"
      | Details -> "detailed receipt"
      | DetailsFail -> "failed getting receipt details"
      | Upload -> "synced up receipt"
      | UploadFail -> "receipt syncronization failed"

    View.Label(
      text = sprintf "%A %s" localTime state,
      padding = Thickness 5.,
      textColor = if Option.isSome receipt.Error then Color.Accent else Color.Default
     )

  let mutable lastScannedText = ""

  let view model dispatch =
    match model.CurrentView with
    | Settings ->
      View.ContentPage(
        content = View.StackLayout(padding = Thickness 20., verticalOptions = LayoutOptions.Center,
          children = [
            View.Label(text = "Settings", fontSize = FontSize.Named NamedSize.Title)
            View.Label(text = "OFD-registered phone")
            View.Editor(text = model.Phone, textChanged = (fun x -> dispatch (PhoneChanged x.NewTextValue)), keyboard = Keyboard.Telephone)
            if not (phoneValid.IsMatch model.Phone) then
              View.Label(text = "Phone format: +7XXXYYYZZZZ", textColor = Color.Accent)
            View.Label(text = "OFD password")
            View.Editor(text = model.Password, textChanged = (fun x -> dispatch (PasswordChanged x.NewTextValue)), keyboard = Keyboard.Numeric)
            if not (paswdValid.IsMatch model.Password) then
              View.Label(text = "Password consists of 6 digits", textColor = Color.Accent)
            View.Label(text = "Your MS Excel OneDrive sharing link")
            View.Editor(text = model.SharingLink, textChanged = (fun x -> dispatch (SharingLinkChanged x.NewTextValue)), keyboard = Keyboard.Url)
            View.Button(text = "Save&Exit", command = fun () -> dispatch SaveSettings)
            if ValueOption.isSome model.SettingsSaveError then
              View.Label(text = (ValueOption.get model.SettingsSaveError), textColor = Color.Accent)
          ]
        )
      )
    | Main ->
      View.ContentPage(
        content = View.FlexLayout(padding = Thickness 20., direction = FlexDirection.Column,
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
      )
    | Scanner ->
      View.ContentPage(
        content =
          View.BarCodeScanner(
            onScanResult =
              fun res ->
                let text = res.Text
                if lastScannedText <> text then
                  lastScannedText <- text
                  dispatch (Scanned text)
          )
      )

    // Note, this declaration is needed if you enable LiveUpdate
  let program = XamarinFormsProgram.mkProgram init update view

type App (backgroundRunner: (CancellationToken -> unit) -> unit, activityOrWindow: obj) as app = 
    inherit Application ()

    do MSALSignIn.parentActivityOrWindow <- activityOrWindow

    do backgroundRunner (fun ct -> Async.RunSynchronously(ReceiptsPipeline.run(), cancellationToken = ct))

    let subscribeOnDbChanges dispatch =
      Receipts.onDbUpdate.Publish.Subscribe(fun () -> dispatch (App.updateReceipts()))
      |> ignore

    let subscribeOnAuthRequirance dispatch =
      MSALAuthEvents.onAuthRequired.Publish.Subscribe(fun () -> dispatch App.RequireMSALSignIn)
      |> ignore

    let runner = 
        App.program
        |> Program.withSubscription (fun _ -> Cmd.ofSub subscribeOnDbChanges)
        |> Program.withSubscription (fun _ -> Cmd.ofSub subscribeOnAuthRequirance)
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/tools.html#live-update for further  instructions.
    //
    do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/models.html#saving-application-state for further  instructions.
#if APPSAVE
    let modelId = "model"
    override __.OnSleep() = 

        let json = Newtonsoft.Json.JsonConvert.SerializeObject(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = Newtonsoft.Json.JsonConvert.DeserializeObject<App.Model>(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()
#endif


