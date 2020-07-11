module ScannerView

  open Fabulous.XamarinForms
  open Update
  
  let mutable private lastScannedText = ""

  let view model dispatch =
    View.BarCodeScanner(
      onScanResult =
        fun res ->
          let text = res.Text
          if lastScannedText <> text then
            lastScannedText <- text
            dispatch (Scanned text)
    )