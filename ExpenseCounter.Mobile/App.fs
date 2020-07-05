// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace ExpenseCounter.Mobile

open System.Diagnostics
open System.Text.RegularExpressions
open Fabulous
open Fabulous.XamarinForms
open Fabulous.XamarinForms.LiveUpdate
open Xamarin.Forms

module App = 
  type Model =
    {
      SettingsOpen: bool
      Phone: string
      Password: string
    }

  type Msg =
    | OpenSettings
    | LoadedCredentialsFromStorage of OfdCredentials.OfdCredentials ValueOption
    | PhoneChanged of string
    | PasswordChanged of string
    | SaveCredentials
    | CredentialsSaved

  let initModel =
    {
      SettingsOpen = false
      Phone = "+7"
      Password = ""
    }

  let private loadFromStorage =
    async {
      let! credentials = OfdCredentials.get()
      return LoadedCredentialsFromStorage credentials
    }
    |> Cmd.ofAsyncMsg

  let init () = initModel, Cmd.none

  let private saveToStorage model =
    async {
      do! OfdCredentials.set { Phone = model.Phone; Password = model.Password }
      return CredentialsSaved
    }
    |> Cmd.ofAsyncMsg

  let update msg model =
    match msg with
    | OpenSettings -> { model with SettingsOpen = true }, loadFromStorage
    | LoadedCredentialsFromStorage cred ->
      cred
      |> ValueOption.map (fun x -> { model with Phone = x.Phone; Password = x.Password })
      |> ValueOption.defaultValue model
      , Cmd.none
    | PhoneChanged phone -> { model with Phone = phone }, Cmd.none
    | PasswordChanged pwd -> { model with Password = pwd }, Cmd.none
    | SaveCredentials -> model, saveToStorage model
    | CredentialsSaved -> { model with SettingsOpen = false; Phone = "+7"; Password = "" }, Cmd.none

  let phoneValid = Regex(@"^\+7\d{3}\d{7}$")
  let paswdValid = Regex(@"^\d{6}$")

  let view model dispatch =
    if model.SettingsOpen then
      View.ContentPage(
        content = View.StackLayout(padding = Thickness 20., verticalOptions = LayoutOptions.Center,
          children = [
            View.Label(text = "Settings", fontSize = FontSize.Named NamedSize.Title)
            View.Label(text = "OFD-registered phone")
            View.Editor(text = model.Phone, textChanged = (fun x -> dispatch (PhoneChanged x.NewTextValue)), keyboard = Keyboard.Telephone)
            if not (phoneValid.IsMatch model.Phone) then
              View.Label(text="Phone format: +7XXXYYYZZZZ", textColor = Color.Accent)
            View.Label(text = "OFD password")
            View.Editor(text = model.Password, textChanged = (fun x -> dispatch (PasswordChanged x.NewTextValue)), keyboard = Keyboard.Numeric)
            if not (paswdValid.IsMatch model.Password) then
              View.Label(text="Password consists of 6 digits", textColor = Color.Accent)
            View.Button(text = "Save&Exit", command = fun () -> dispatch SaveCredentials)
          ]
        )
      )
    else
      View.ContentPage(
        content = View.StackLayout(padding = Thickness 20., //verticalOptions = LayoutOptions.Center,
          children = [
            View.ImageButton(
              source = ImageSrc (ImageSource.FromResource("ExpenseCounter.Mobile.icon_settings.png", typeof<Msg>.Assembly)),
              command = (fun () -> dispatch OpenSettings),
              width = 64.0,
              height = 64.0,
              aspect = Aspect.AspectFit,
              horizontalOptions = LayoutOptions.End,
              backgroundColor = Color.Transparent
            )
          ]
        )
      )

    // Note, this declaration is needed if you enable LiveUpdate
  let program = XamarinFormsProgram.mkProgram init update view

type App () as app = 
    inherit Application ()

    let runner = 
        App.program
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


