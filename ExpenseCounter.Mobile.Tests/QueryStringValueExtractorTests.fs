module QueryStringValueExtractorTests

open Expecto
open System
open System.Collections.Generic

[<Tests>]
let tests =
  testList "query string value extraction tests" [

    testCase "unknown key throws KeyNotFound" <| fun _ ->
      let qs = "a=abc&b=def&c=ghi"
      let key = "lol"
      let execution () = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs |> ignore
      Expect.throwsT<KeyNotFoundException> execution "Should throw key not found exception on unknown key"
      
    testCase "unknown key throws KeyNotFound even if the key is presented as value" <| fun _ ->
      let qs = "a=abc&b=lol&c=ghi"
      let key = "lol"
      let execution () = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs |> ignore
      Expect.throwsT<KeyNotFoundException> execution "Should throw key not found exception on unknown key"
      
    testCase "extract empty string when it is the only value" <| fun _ ->
      let qs = "a="
      let key = "a"
      let expectedValue = ""
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs
      Expect.equal value expectedValue "Should extract the correct value"
      
    testCase "extract empty string when it stands before another key" <| fun _ ->
      let qs = "a=&b=23"
      let key = "a"
      let expectedValue = ""
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs
      Expect.equal value expectedValue "Should extract the correct value"

    testCase "extract string from beginning of the string" <| fun _ ->
      let qs = "a=abc&b=def&c=ghi"
      let key = "a"
      let expectedValue = "abc"
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs
      Expect.equal value expectedValue "Should extract the correct value"

    testCase "extract string from middle of the string" <| fun _ ->
      let qs = "a=abc&b=def&c=ghi"
      let key = "b"
      let expectedValue = "def"
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs
      Expect.equal value expectedValue "Should extract the correct value"

    testCase "extract string from end of the string" <| fun _ ->
      let qs = "a=abc&b=def&c=ghi"
      let key = "c"
      let expectedValue = "ghi"
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.stringReviver qs
      Expect.equal value expectedValue "Should extract the correct value"

    testCase "non DateTime value throws format exception if value is not DateTime" <| fun _ ->
      let qs = "a=abc&b=def&c=ghi"
      let key = "a"
      let execution () = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.dateTimeReviver qs |> ignore
      Expect.throwsT<FormatException> execution "Should throw format exception on non-DateTime value"
      
    testCase "extract DateTime from beginning of the string" <| fun _ ->
      let qs = "a=20200701T1313&b=def&c=ghj"
      let key = "a"
      let expectedValue = DateTime(2020, 07, 01, 13, 13, 0, DateTimeKind.Unspecified)
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.dateTimeReviver qs
      Expect.equal value expectedValue "Should extract the correct value"
      
    testCase "extract DateTime from middle of the string" <| fun _ ->
      let qs = "a=abc&b=20200701T1313&c=ghj"
      let key = "b"
      let expectedValue = DateTime(2020, 07, 01, 13, 13, 0, DateTimeKind.Unspecified)
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.dateTimeReviver qs
      Expect.equal value expectedValue "Should extract the correct value"
      
    testCase "extract DateTime from end of the string" <| fun _ ->
      let qs = "a=abc&b=def&c=20200701T1313"
      let key = "c"
      let expectedValue = DateTime(2020, 07, 01, 13, 13, 0, DateTimeKind.Unspecified)
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.dateTimeReviver qs
      Expect.equal value expectedValue "Should extract the correct value"

    testCase "non decimal value throws format exception if value is not decimal" <| fun _ ->
      let qs = "a=abc&b=def&c=ghi"
      let key = "a"
      let execution () = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.decimalReviver qs |> ignore
      Expect.throwsT<FormatException> execution "Should throw format exception on non-decimal value"
      
    testCase "extract decimal from beginning of the string" <| fun _ ->
      let qs = "a=21.34&b=def&c=ghj"
      let key = "a"
      let expectedValue = 21.34m
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.decimalReviver qs
      Expect.equal value expectedValue "Should extract the correct value"
      
    testCase "extract decimal from middle of the string" <| fun _ ->
      let qs = "a=abc&b=21.34&c=ghj"
      let key = "b"
      let expectedValue = 21.34m
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.decimalReviver qs
      Expect.equal value expectedValue "Should extract the correct value"
      
    testCase "extract decimal from end of the string" <| fun _ ->
      let qs = "a=abc&b=def&c=21.34"
      let key = "c"
      let expectedValue = 21.34m
      let value = QueryStringValueExtractor.extractQueryStringComponent key QueryStringValueExtractor.decimalReviver qs
      Expect.equal value expectedValue "Should extract the correct value"
  ]