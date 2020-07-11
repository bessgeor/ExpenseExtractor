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

  let handleDetailable detailable =
    async {
      for receipt in detailable do
        let! detailed = tryDetail receipt.Receipt
        updateReceipt receipt.Id detailed
    }

  let handleUploadable uploadable =
    async {
      let! uploaded = tryUpload uploadable
      for uploaded in uploaded do
        updateReceipt uploaded.Id uploaded.Receipt
    }

  let retry receipt =
    async {
      let currentState = getReceipt receipt.Id
      match currentState.Stage with
      | ParsingError -> do handleParsable (seq { currentState })
      | DetailsFail -> do! handleDetailable (seq { currentState })
      | UploadFail -> do! handleUploadable (seq { currentState })
      | _ -> do ignore()
    }

  let run () =
    async {
      while true do
        do getReceiptsOnStep Scan |> handleParsable
        do! getReceiptsOnStep Parse |> handleDetailable
        do! getReceiptsOnStep Details |> handleUploadable
        do! Async.Sleep 100
    }
