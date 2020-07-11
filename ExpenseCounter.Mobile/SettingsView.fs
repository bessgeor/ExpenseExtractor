module SettingsView

  open Fabulous.XamarinForms
  open Model
  open Update
  open Xamarin.Forms
  open System.Text.RegularExpressions
  
  
  let private phoneValid = Regex(@"^\+7\d{3}\d{7}$")
  let private paswdValid = Regex(@"^\d{6}$")

  let view model dispatch =
    View.StackLayout(padding = Thickness 20., verticalOptions = LayoutOptions.Center,
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
