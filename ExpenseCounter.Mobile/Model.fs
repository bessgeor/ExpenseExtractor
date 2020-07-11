module Model
  type Views =
    | Main
    | Settings
    | Scanner
    | ReceiptDetails of Receipts.ReceiptDTO

  type Model =
    {
      CurrentView: Views
      Phone: string
      Password: string
      SharingLink: string
      SettingsSaveError: string voption

      CurrentReceipts: Receipts.ReceiptDTO array
    }

  let initModel =
    {
      CurrentView = Main
      Phone = "+7"
      Password = ""
      SharingLink = ""
      SettingsSaveError = ValueNone

      CurrentReceipts = [||]
    }

