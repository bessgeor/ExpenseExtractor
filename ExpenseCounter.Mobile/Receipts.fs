module Receipts

open System
open LiteDB
open LiteDB.FSharp
open LiteDB.FSharp.Extensions

  type ParsedReceipt =
    {
      Time: DateTime
      Sum: decimal
      FiscalNumber: string // 16-digit ID of fiscal register device
      FiscalDocumentNumber: string // sequential number of the receipt on the device, up to 10 digits
      FiscalSignature: string // some kind of cryptographic signature for the data above
    }

  type Position =
    {
      Name: string
      Quantity: double
      Price: decimal
      Sum: decimal
    }

  type ReceiptDetails =
    {
      Identifiers: ParsedReceipt
      SellerTIN: string
      RetailAddress: string option
      StoreName: string option
      IssuedAt: DateTime
      Positions: Position array
    }

  type Receipt =
    | RawScanned of string //t=20200701T1313&s=552.00&fn=9252440300187682&i=11854&fp=4003497372&n=1
    | ParseFailed of string * Exception
    | Parsed of ParsedReceipt
    | Detailed of ReceiptDetails
    | DetailsGettingFailed of ParsedReceipt * Exception
    | Uploaded of ReceiptDetails
    | UploadFailed of ReceiptDetails * Exception

  type PipelineStep =
    | Scan
    | Parse
    | ParsingError
    | Details
    | DetailsFail
    | Upload
    | UploadFail

  [<CLIMutable>]
  type ReceiptDTO =
    {
      Id: int
      LastAction: DateTime
      Stage: PipelineStep
      Error: Exception option
      Receipt: Receipt
    }

  let stageToString receipt =
    match receipt.Stage with
    | Scan -> "scanned receipt"
    | Parse -> "parsed receipt"
    | ParsingError -> "receipt parse failed"
    | Details -> "detailed receipt"
    | DetailsFail -> "failed getting receipt details"
    | Upload -> "synced up receipt"
    | UploadFail -> "receipt syncronization failed"


  let private mapper = FSharpBsonMapper()
  let private dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "receipts.db")
  let private accessDb () = new LiteDatabase(dbPath, mapper)
  let private getCollection (db: LiteDatabase) =
    let collection = db.GetCollection<ReceiptDTO>("receipts")
    let x = Unchecked.defaultof<ReceiptDTO>
    collection.EnsureIndex((nameof x.Stage), false) |> ignore
    collection

  let onDbUpdate = Event<ReceiptDTO voption>()

  let addReceiptFromScan scanned =
    use db = accessDb()
    let collection = getCollection db
    let receipt = RawScanned scanned
    let oldReceipt =
      collection.fullSearch <@ fun x -> x.Receipt @> (fun r -> r = receipt)
      |> Seq.tryHead
    if Option.isSome oldReceipt then
      Option.get oldReceipt
    else
      let dto =
        {
          Id = 0
          LastAction = DateTime.UtcNow
          Stage = Scan
          Error = None
          Receipt = receipt
        }
      collection.Insert dto |> ignore
      onDbUpdate.Trigger(ValueSome dto)
      dto

  let updateReceipt (value: ReceiptDTO) =
    use db = accessDb()
    let collection = getCollection db
    collection.Update value |> ignore
    onDbUpdate.Trigger(ValueSome value)
    
  let deleteReceipt (value: ReceiptDTO) =
    use db = accessDb()
    let collection = getCollection db
    collection.delete <@ fun x -> x.Id = value.Id @> |> ignore
    onDbUpdate.Trigger(ValueNone)

  let getReceipt id =
    use db = accessDb()
    let collection = getCollection db
    collection.findOne <@ fun x -> x.Id = id @>

  let getReceiptsOnStep step =
    use db = accessDb()
    let collection = getCollection db
    collection
      .findMany <@ fun x -> x.Stage = step @>
      |> Seq.toArray

  let getLatestReceipts (limit: int) =
    use db = accessDb()
    let collection = getCollection db
    let mutable items = 0
    collection.FindAll()
      |> Seq.sortByDescending (fun x -> x.LastAction)
      |> Seq.takeWhile (fun _ -> items <- items + 1; items < limit)
      |> Seq.toArray
