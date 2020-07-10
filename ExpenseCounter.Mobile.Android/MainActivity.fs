// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace ExpenseCounter.Mobile.Android

open System
open System.Threading

open Android.App
open Android.Content
open Android.Content.PM
open Android.Runtime
open Android.Views
open Android.Widget
open Android.OS
open Xamarin.Forms.Platform.Android
open Microsoft.Identity.Client

[<Activity (Label = "ExpenseCounter.Mobile.Android", Icon = "@drawable/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = (ConfigChanges.ScreenSize ||| ConfigChanges.Orientation))>]
type MainActivity() =
    inherit FormsAppCompatActivity()
    
    member val _bgCancellation: CancellationTokenSource ValueOption = ValueNone with get, set

    override this.OnCreate (bundle: Bundle) =
        FormsAppCompatActivity.TabLayoutResource <- Resources.Layout.Tabbar
        FormsAppCompatActivity.ToolbarResource <- Resources.Layout.Toolbar
        base.OnCreate (bundle)

        Xamarin.Essentials.Platform.Init(this, bundle)

        Xamarin.Forms.Forms.Init (this, bundle)
        
        if (ValueOption.isNone this._bgCancellation) then this._bgCancellation <- new CancellationTokenSource() |> ValueSome

        let background = Thread(ParameterizedThreadStart(fun bg -> (bg :?> (CancellationToken -> unit))((ValueOption.get this._bgCancellation).Token)))
        
        let appcore  = new ExpenseCounter.Mobile.App((fun bg -> background.Start (box bg)), box this)
        this.LoadApplication (appcore)

    override _.OnActivityResult (requestCode, resultCode, data) =
      base.OnActivityResult(requestCode, resultCode, data)
      AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data)

    override this.OnDestroy () =
      this._bgCancellation
      |> ValueOption.map (fun c ->
        c.Cancel()
        c.Dispose()
        this._bgCancellation <- ValueNone
      )
      |> ValueOption.defaultValue (ignore())

    override _.OnRequestPermissionsResult(requestCode: int, permissions: string[], [<GeneratedEnum>] grantResults: Android.Content.PM.Permission[]) =
        Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults)

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults)
