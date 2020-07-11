module Update
  open Model
  open Receipts
  open Fabulous
  open System
  
  type Msg =
    | Navigate of Views
    | LoadedSettingsFromStorage of OfdCredentials.OfdCredentials voption * string voption
    | PhoneChanged of string
    | PasswordChanged of string
    | SharingLinkChanged of string
    | SaveSettings
    | SettingsSaveError of string
    | SettingsSaved

    | ReceiptChanged of ReceiptDTO
    | ReceiptsUpdated of ReceiptDTO array
    | RequireMSALSignIn
    | MSALSignedIn

    | Scanned of string
    | Retry of ReceiptDTO
    
  let updateReceipts () =
    Receipts.getLatestReceipts 10
    |> ReceiptsUpdated
  
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

  let private retry receipt =
    Async.Start (ReceiptsPipeline.retry receipt)
    None


  let update msg model =
    match msg with
    | Navigate view -> { model with CurrentView = view }, loadCredentialsFromStorage
    | ReceiptChanged changed ->
      match model.CurrentView with
      | ReceiptDetails receipt when receipt.Id = changed.Id -> model, Cmd.ofMsg (Navigate (ReceiptDetails changed))
      | _ -> model, Cmd.none
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
        let receipt = Receipts.addReceiptFromScan text
        return Navigate (ReceiptDetails receipt)
      }
    | Retry receipt -> model, Cmd.ofMsgOption (retry receipt)
