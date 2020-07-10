module MSALSignIn

  open System
  open Microsoft.Identity.Client
  open Xamarin.Essentials

  let clientId = "c67273af-fef4-4984-86cf-b1a95b305542"
  let scopes = [| "User.Read"; "Files.ReadWrite"; "Files.ReadWrite.All" |]

  let mutable parentActivityOrWindow: obj = null

  let PCA =
    PublicClientApplicationBuilder
      .Create(clientId)
      .WithRedirectUri(sprintf "msal%s://auth" clientId)
      .Build()

  let private signInAndGetToken () =
    async {
      let! accounts = PCA.GetAccountsAsync() |> Async.AwaitTask
      let account =
        accounts
        |> Seq.tryHead
        |> Option.toObj
      try
        let! cachedAuth = PCA.AcquireTokenSilent(scopes, account).ExecuteAsync() |> Async.AwaitTask
        return ValueSome cachedAuth.AccessToken
      with
      | :? MsalUiRequiredException ->
        try
          let! asquiredAuth =
            PCA
              .AcquireTokenInteractive(scopes)
              .WithParentActivityOrWindow(parentActivityOrWindow)
              .ExecuteAsync()
              |> Async.AwaitTask
          return ValueSome asquiredAuth.AccessToken
        with
        | :? Exception as e -> return ValueNone
      | :? Exception as e -> return ValueNone
    }

  let private secureStorageKey = "msal-access-token"
  let private set tokenOption =
    async {
      match tokenOption with
      | ValueSome token -> do! SecureStorage.SetAsync(secureStorageKey, token) |> Async.AwaitTask
      | ValueNone -> ignore()
    }
  let get () =
    async {
      let! token = SecureStorage.GetAsync secureStorageKey |> Async.AwaitTask
      return ValueOption.ofObj token
    }

  let signIn () =
    async {
      let! token = signInAndGetToken()
      do! set token
      match token with
      | ValueSome _ -> do MSALAuthEvents.onAuthSuccess.Trigger()
      | ValueNone -> ignore()
    }