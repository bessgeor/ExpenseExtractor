module MSGraphAPI
  open Microsoft.Graph
  open Microsoft.Graph.Auth
  open System.Net
  open System.Net.Http
  open System.Text
  open System

  let authProvider = InteractiveAuthenticationProvider(MSALSignIn.PCA, MSALSignIn.scopes)

  type private OnAuthFailureHandler () =
    inherit DelegatingHandler()

    member private this.DoSending requestMessage ct =
      let responseAsync = base.SendAsync(requestMessage, ct)
      async {
        let! response = responseAsync |> Async.AwaitTask

        if response.StatusCode = HttpStatusCode.Unauthorized then
          MSALAuthEvents.onAuthRequired.Trigger()
          do! Async.AwaitEvent MSALAuthEvents.onAuthSuccess.Publish
          let! response = this.DoSending requestMessage ct
          return response
        else
          return response
      }

    override this.SendAsync (requestMessage, ct) = 
      this.DoSending requestMessage ct
      |> fun a -> Async.StartAsTask (a, cancellationToken = ct)

  let private authHandler = new OnAuthFailureHandler()

  let handlers = GraphClientFactory.CreateDefaultHandlers authProvider
  handlers.Add authHandler

  let httpClient = GraphClientFactory.Create(handlers, nationalCloud = GraphClientFactory.Global_Cloud)

  let tempClient = GraphServiceClient authProvider

  type private HttpProvider () =
    member val private timeout = tempClient.HttpProvider.OverallTimeout with get, set

    interface IHttpProvider with
      member this.OverallTimeout
        with get () = this.timeout
        and set v = this.timeout <- v
      member _.Serializer = tempClient.HttpProvider.Serializer
      member _.SendAsync message = httpClient.SendAsync message
      member _.SendAsync (message, options, ct) = httpClient.SendAsync (message, options, ct)
      member _.Dispose () = httpClient.Dispose()

  let client = GraphServiceClient (authProvider, (new HttpProvider()))

  let getError (e: #Entity) =
    let (hasError, error) = e.AdditionalData.TryGetValue "error"
    if hasError then
      ValueSome error
    else ValueNone

  let throwOnError (e: #Entity) =
    let error = getError e
    match error with
    | ValueSome err -> raise (Exception (err.ToString()))
    | ValueNone -> ignore()

  let requestSharedFile encodedSharingLink =
    async {
      let! driveItem = client.Shares.Item(encodedSharingLink).DriveItem.Request().GetAsync() |> Async.AwaitTask
      do throwOnError driveItem
      return driveItem
    }
  