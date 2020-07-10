namespace ExpenseCounter.Mobile.Android

open Android.App
open Android.Content
open Microsoft.Identity.Client

[<Activity (Label = "MsalActivity")>]
[<IntentFilter([| Intent.ActionView |],
  Categories = [| Intent.CategoryBrowsable; Intent.CategoryDefault |],
  DataHost = "auth",
  DataScheme = "msalc67273af-fef4-4984-86cf-b1a95b305542")>]
type MsalActivity () =
  inherit BrowserTabActivity()
