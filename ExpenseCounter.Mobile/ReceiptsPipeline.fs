module ReceiptsPipeline
  open Receipts
  open ReceiptsPipelineFunctions

  let private statusFromState =
    function
    | RawScanned -> Scan, None
    | Parsed -> Parse, None
    | ParseFailed (_, err) -> ParsingError, Some err
    | Detailed -> Details, None
    | DetailsGettingFailed (_, err) -> DetailsFail, Some err
    | Uploaded -> Upload, None
    | UploadFailed (_, err) -> UploadFail, Some err

  let private updateReceipt id receipt =
    let stage, err = statusFromState receipt
    updateReceipt {
      Id = id
      Receipt = receipt
      Stage = stage;
      Error = err;
      LastAction = System.DateTime.UtcNow
    }

  let handleParsable parsable =
    for receipt in parsable do
      let parsed = tryParse receipt.Receipt
      updateReceipt receipt.Id parsed

  let run () =
    async {
      while true do
        do getReceiptsOnStep Scan |> handleParsable
        do! Async.Sleep 100
    }
