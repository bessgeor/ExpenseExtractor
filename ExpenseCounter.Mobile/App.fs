// Copyright 2018-2019 Fabulous contributors. See LICENSE.md for license.
namespace ExpenseCounter.Mobile

open Fabulous
open Fabulous.XamarinForms
open Fabulous.XamarinForms.LiveUpdate
open Xamarin.Forms
open System.Threading
open System
open Microsoft.FSharpLu.Json

module App =
  open Model
  open Update

  let init () = initModel, Cmd.ofMsg (updateReceipts())

  let view model dispatch =
    View.ContentPage(
      content = 
        (
          match model.CurrentView with
          | Main -> MainView.view
          | Settings -> SettingsView.view
          | Scanner -> ScannerView.view
          | ReceiptDetails -> ReceiptDetailsView.view
        ) model dispatch
    )

  // Note, this declaration is needed if you enable LiveUpdate
  let program = XamarinFormsProgram.mkProgram init update view

type App (backgroundRunner: (CancellationToken -> unit) -> unit, activityOrWindow: obj) as app = 
    inherit Application ()

    do MSALSignIn.parentActivityOrWindow <- activityOrWindow

    do backgroundRunner (fun ct -> Async.RunSynchronously(ReceiptsPipeline.run(), cancellationToken = ct))
    
    let subscribeListOnDbChanges dispatch =
      Receipts.onDbUpdate.Publish.Subscribe(fun _ -> dispatch (Update.updateReceipts()))
      |> ignore
    
    let subscribeDetailsOnDbChanges dispatch =
      Receipts.onDbUpdate.Publish.Subscribe(fun changed -> 
        if ValueOption.isSome changed then
          dispatch (Update.ReceiptChanged (ValueOption.get changed)))
      |> ignore

    let subscribeOnAuthRequirance dispatch =
      MSALAuthEvents.onAuthRequired.Publish.Subscribe(fun () -> dispatch Update.RequireMSALSignIn)
      |> ignore

    let runner = 
        App.program
        |> Program.withSubscription (fun _ -> Cmd.ofSub subscribeListOnDbChanges)
        |> Program.withSubscription (fun _ -> Cmd.ofSub subscribeOnAuthRequirance)
        |> Program.withSubscription (fun _ -> Cmd.ofSub subscribeDetailsOnDbChanges)
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> XamarinFormsProgram.run app

#if DEBUG
    // Uncomment this line to enable live update in debug mode. 
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/tools.html#live-update for further  instructions.
    //
    // do runner.EnableLiveUpdate()
#endif    

    // Uncomment this code to save the application state to app.Properties using Newtonsoft.Json
    // See https://fsprojects.github.io/Fabulous/Fabulous.XamarinForms/models.html#saving-application-state for further  instructions.

    let modelId = "model"
    override __.OnSleep() = 

        let json = Compact.serialize(runner.CurrentModel)
        Console.WriteLine("OnSleep: saving model into app.Properties, json = {0}", json)

        app.Properties.[modelId] <- json

    override __.OnResume() = 
        Console.WriteLine "OnResume: checking for model in app.Properties"
        try 
            match app.Properties.TryGetValue modelId with
            | true, (:? string as json) -> 

                Console.WriteLine("OnResume: restoring model from app.Properties, json = {0}", json)
                let model = BackwardCompatible.deserialize(json)

                Console.WriteLine("OnResume: restoring model from app.Properties, model = {0}", (sprintf "%0A" model))
                runner.SetCurrentModel (model, Cmd.none)

            | _ -> ()
        with ex -> 
            App.program.onError("Error while restoring model found in app.Properties", ex)

    override this.OnStart() = 
        Console.WriteLine "OnStart: using same logic as OnResume()"
        this.OnResume()

