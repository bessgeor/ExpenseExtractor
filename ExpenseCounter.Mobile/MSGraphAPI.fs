module MSGraphAPI
  open Microsoft.Graph
  open Microsoft.Graph.Auth
  open System.Net
  open System.Net.Http
  open System.Text
  open System

  let authProvider = InteractiveAuthenticationProvider(MSALSignIn.PCA, MSALSignIn.scopes)

  let client = GraphServiceClient authProvider

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

  type AsyncBuilder with
    member inline private this.authAndRetry computation binder =
      do Async.Start (async { MSALAuthEvents.onAuthRequired.Trigger() })
      this.Bind(
        this.Delay(fun () -> Async.AwaitEvent MSALAuthEvents.onAuthSuccess.Publish),
        fun () -> this.Bind (computation, binder)
      )

    [<CustomOperation("authenticated", AllowIntoPattern = true, MaintainsVariableSpaceUsingBind = true)>]
    member this.Authenticated<'T, 'U when 'U :> Entity> (computation: Async<'T>, [<ProjectionParameter>]binder:('T -> Async<'U>)) =
      this.TryWith (
        this.Bind (computation, binder),
        (function 
          | :? Microsoft.Graph.Auth.AuthenticationException -> this.authAndRetry computation binder
          | :? AggregateException as aggr ->
            match aggr.InnerException with
            | :? Microsoft.Graph.ServiceException as serv when serv.Error.Code = "generalException" ->
              this.authAndRetry computation binder
            | exn -> raise exn
          | exn -> raise exn
        )
      )

  let requestSharedFile encodedSharingLink =
    async {
      authenticated (client.Shares.Item(encodedSharingLink).DriveItem.Request().GetAsync() |> Async.AwaitTask) into driveItem
      do throwOnError driveItem
      return driveItem
    }
  