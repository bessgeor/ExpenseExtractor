module SharingLink

  open System
  open System.Text
  open Xamarin.Essentials

  let private sharingLinkSecureStorageKey = "document-sharing-link"
  let private encodedSharingLinkSecureStorageKey = "encoded-document-sharing-link"
  
  // consider rewrite to String.Create/StringBuilder if need to reduce allocations (probably never or just for fun)
  let encodeSharingLink (unencodedSharingLink: string) =
    let base64Value = Encoding.UTF8.GetBytes unencodedSharingLink |> Convert.ToBase64String
    "u!" + base64Value.TrimEnd('=').Replace('/','_').Replace('+','-');

  let setUnencodedSharingLink link =
    async {
      let encoded = encodeSharingLink link
      do! SecureStorage.SetAsync(sharingLinkSecureStorageKey, link) |> Async.AwaitTask
      do! SecureStorage.SetAsync(encodedSharingLinkSecureStorageKey, encoded) |> Async.AwaitTask
    }

  let getEncodedSharingLink () =
    async {
      let! encoded = SecureStorage.GetAsync encodedSharingLinkSecureStorageKey |> Async.AwaitTask
      return ValueOption.ofObj encoded
    }

  let getUnencodedSharingLink () =
    async {
      let! unencoded = SecureStorage.GetAsync sharingLinkSecureStorageKey |> Async.AwaitTask
      return ValueOption.ofObj unencoded
    }