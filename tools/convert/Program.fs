open Fli
open System
open System.IO

type Size = { W: int32; H: int32 }
type Image = { Name: string; Size: Size }
type Font = { Id: string; PointSize: int32 }

[<Literal>]
let ALPHABET =
    @" !""#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\]^_`abcdefghijklmnopqrstuvwxyz{|}~?"

[<Literal>]
let DEFAULT_POINT_SIZE = 14

let appendImages (images: array<Image>) (font: Font) : Unit =
    assert (ALPHABET.Length = images.Length)

    let input = images |> Array.map _.Name |> String.concat " "
    let output = $"{font.Id}-{font.PointSize}.png".ToLower()

    cli {
        Exec "magick"
        Arguments $"{input} +append {output}"
    }
    |> Command.execute
    |> Output.printError

let findMostFrequentSize (images: array<Image>) : Size =
    images |> Array.map _.Size |> Array.countBy id |> Array.maxBy snd |> fst

let cropImagesToMutualSize (images: array<Image>) : Unit =
    let mostFrequentSize = findMostFrequentSize (images)

    images
    |> Array.filter (fun img -> not (img.Size = mostFrequentSize))
    |> Array.iter (fun img ->
        cli {
            Exec "magick"
            Arguments $"mogrify -gravity Center -crop {mostFrequentSize.W}x{mostFrequentSize.H}+0+0 +repage {img.Name}"
        }
        |> Command.execute
        |> ignore)

let rec createImage (dirName: string) (font: Font) (i: int32) (result: array<Image>) =
    if (i = ALPHABET.Length) then
        result

    else
        let outputFile = Path.Join(dirName, $"char_{i:D2}.png")

        let labelText =
            match ALPHABET[i] with
            | ' ' -> @"\ "
            | '"' -> @"\"""
            | '\\' -> @"\\\\"
            | _ as noSpecial -> string noSpecial

        cli {
            Exec "magick"
            Arguments
                $"""-background none -font {font.Id} -pointsize {font.PointSize} -gravity Center label:"{labelText}" {outputFile}"""
        }
        |> Command.execute
        |> ignore

        printf $"{i}.) "

        let sizeLine =
            cli {
                Exec "magick"
                Arguments $"identify -ping -format %%wx%%h {outputFile}"
            }
            |> Command.execute
            |> Output.toText

        printfn $"{sizeLine}"

        let size = sizeLine.Split('x') |> Array.map int32
        assert (size.Length = 2)

        let width = size[0]
        let height = size[1]

        createImage
            dirName
            font
            (i + 1)
            (Array.append
                result
                [| { Name = outputFile
                     Size = { W = width; H = height } } |])

let checkFontAvailability (font: string) : bool =
    let magickFontOutput =
        cli {
            Exec "magick"
            Arguments "-list font"
        }
        |> Command.execute
        |> Output.toText

    let availableFonts =
        magickFontOutput.Split('\n')
        |> Array.map (fun line -> line.Trim())
        |> Array.filter (fun line -> line.StartsWith("Font: "))
        |> Array.map (fun line -> line.Replace("Font: ", String.Empty))

    availableFonts |> Array.contains (font)

let convertFontToPng (font: Font) : Unit =
    let imageDir = Directory.CreateDirectory("ascii_images")
    let images = createImage imageDir.Name font 0 Array.empty
    cropImagesToMutualSize images
    appendImages images font

let runScript (options: array<string>) : Result<Unit, string> =
    if Array.isEmpty options || options.Length > 2 then
        Error ("How to use")

    else
        let font = options[0]
        let isValidFont = checkFontAvailability (font)

        if not (isValidFont) then
            Error ("Not valid font")

        else
            let pointSize = 
                if options.Length = 2 then
                    match Int32.TryParse options[1] with
                    | true, num when num > 0 -> Some(num)
                    | _ -> None

                else
                    Some(DEFAULT_POINT_SIZE)

            if pointSize.IsNone then
                Error ("Invalid pointsize")
        
            else
                Ok (convertFontToPng { Id = font; PointSize = pointSize.Value })

[<EntryPoint>]
let main args : int32 =
    match runScript(args) with
    | Error message -> printfn $"{message}"
    | _ -> printfn "success"
    
    0
