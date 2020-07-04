module OfdCredentials

open Xamarin.Essentials

  [<Struct>]
  type OfdCredentials = {
    Phone: string
    Password: string
  }

  let private ofdCredentials phone pwd =
    {
      Phone = phone
      Password = pwd
    }

  let private phoneKey = "ofd_credentials_phone"
  let private paswdKey = "ofd_credentials_password"

  let get () =
    async {
      let! phone = SecureStorage.GetAsync phoneKey |> Async.AwaitTask
      let phoneOpt = ValueOption.ofObj phone
      if ValueOption.isNone phoneOpt
        then return ValueNone
        else
          let! paswd = SecureStorage.GetAsync paswdKey |> Async.AwaitTask
          let paswdOpt = ValueOption.ofObj paswd
          return ValueOption.map2 ofdCredentials phoneOpt paswdOpt
    }

  let set credentials =
    async {
      do! SecureStorage.SetAsync(phoneKey, credentials.Phone) |> Async.AwaitTask
      do! SecureStorage.SetAsync(paswdKey, credentials.Password) |> Async.AwaitTask
    }