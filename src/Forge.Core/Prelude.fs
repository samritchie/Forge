﻿[<AutoOpen>]
module Forge.Prelude

open System
open System.IO
open System.Diagnostics
open System.Xml


// Operators
//========================


let (^) = (<|)

let inline (|?|) (pred1:'a->bool) (pred2:'a->bool)  =
    fun a -> pred1 a || pred2 a

let inline (|&|) (pred1:'a->bool) (pred2:'a->bool)  =
    fun a -> pred1 a && pred2 a

/// Combines two path strings using Path.Combine
let inline combinePaths path1 (path2 : string) = Path.Combine (path1, path2.TrimStart [| '\\'; '/' |])
let inline combinePathsNoTrim path1 path2 = Path.Combine (path1, path2)

/// Combines two path strings using Path.Combine
let inline (@@) path1 path2 = combinePaths path1 path2
let inline (</>) path1 path2 = combinePathsNoTrim path1 path2


// Environment Helpers
//=======================================================


/// Retrieves the environment variable with the given name
let environVar name = Environment.GetEnvironmentVariable name

let environVarOrNone name =
    let var = environVar name
    if String.IsNullOrEmpty var then None
    else Some var

/// Splits the entries of an environment variable and removes the empty ones.
let splitEnvironVar name =
    let var = environVarOrNone name
    if var = None then [ ]
    else var.Value.Split [| Path.PathSeparator |] |> Array.toList

/// The system root environment variable. Typically "C:\Windows"
let SystemRoot = environVar "SystemRoot"

/// Determines if the current system is an Unix system
let isUnix = Environment.OSVersion.Platform = PlatformID.Unix

/// Determines if the current system is a MacOs system
let isMacOS =
    (Environment.OSVersion.Platform = PlatformID.MacOSX) ||
      // osascript is the AppleScript interpreter on OS X
      File.Exists "/usr/bin/osascript"

/// Determines if the current system is a Linux system
let isLinux = int System.Environment.OSVersion.Platform |> fun p -> p=4 || p=6 || p=128

/// Determines if the current system is a mono system
/// Todo: Detect mono on windows
let isMono = isLinux || isUnix || isMacOS

let monoPath =
    if isMacOS && File.Exists "/Library/Frameworks/Mono.framework/Commands/mono" then
        "/Library/Frameworks/Mono.framework/Commands/mono"
    else
        "mono"

/// The path of the "Program Files" folder - might be x64 on x64 machine
let ProgramFiles = Environment.GetFolderPath Environment.SpecialFolder.ProgramFiles

/// The path of Program Files (x86)
/// It seems this covers all cases where PROCESSOR\_ARCHITECTURE may misreport and the case where the other variable
/// PROCESSOR\_ARCHITEW6432 can be null
let ProgramFilesX86 =
    let wow64 = environVar "PROCESSOR_ARCHITEW6432"
    let globalArch = environVar "PROCESSOR_ARCHITECTURE"
    match wow64, globalArch with
    | "AMD64", "AMD64"
    | null, "AMD64"
    | "x86", "AMD64" -> environVar "ProgramFiles(x86)"
    | _ -> environVar "ProgramFiles"
    |> fun detected -> if detected = null then @"C:\Program Files (x86)\" else detected



/// Detects whether the given path does not contains invalid characters.
let isValidPath (path:string) =
    let invalidChars = Path.GetInvalidPathChars()
    (true, path.ToCharArray())
    ||> Array.fold (fun isValid pathChar ->
        if not isValid then false else
        not ^ Array.exists ((=) pathChar) invalidChars
    )

/// Returns if the build parameter with the given name was set
let inline hasBuildParam name = environVar name <> null

/// Type alias for System.EnvironmentVariableTarget
type EnvironTarget = EnvironmentVariableTarget

/// Retrieves all environment variables from the given target
let environVars target =
    [ for e in Environment.GetEnvironmentVariables target ->
          let e1 = e :?> Collections.DictionaryEntry
          e1.Key, e1.Value ]

// String Helpers
//=====================================================
[<RequireQualifiedAccess>]
module String =

    let equalsIgnoreCase (str1:string) (str2:string) =
        String.Compare(str1,str2,StringComparison.OrdinalIgnoreCase) = 0
    /// Converts a sequence of strings to a string with delimiters
    let inline separated delimiter (items : string seq) = String.Join(delimiter, Array.ofSeq items)

    /// Returns if the string is null or empty
    let inline isNullOrEmpty value = String.IsNullOrEmpty value

    /// Returns if the string is not null or empty
    let inline isNotNullOrEmpty value = not ^ String.IsNullOrEmpty value

    /// Returns if the string is null or empty or completely whitespace
    let inline isNullOrWhiteSpace value = isNullOrEmpty value || value |> Seq.forall Char.IsWhiteSpace

    /// Converts a sequence of strings into a string separated with line ends
    let inline toLines text = separated Environment.NewLine text

    /// Checks whether the given text starts with the given prefix
    let startsWith prefix (text : string) = text.StartsWith prefix

    /// Checks whether the given text ends with the given suffix
    let endsWith suffix (text : string) = text.EndsWith suffix

    /// Determines whether the last character of the given <see cref="string" />
    /// matches Path.DirectorySeparatorChar.
    let endsWithSlash = endsWith (Path.DirectorySeparatorChar.ToString())
    /// Reads a file as one text
    let inline readFileAsString file = File.ReadAllText file

    /// Replaces the given pattern in the given text with the replacement
    let inline replace (pattern : string) replacement (text : string) = text.Replace(pattern, replacement)

    /// Replaces the first occurrence of the pattern with the given replacement.
    let replaceFirst (pattern : string) replacement (text : string) =
        let pos = text.IndexOf pattern
        if pos < 0 then text
        else text.Remove(pos, pattern.Length).Insert(pos, replacement)

    /// Trims the given string with the DirectorySeparatorChar
    let inline trimSeparator (s : string) = s.TrimEnd Path.DirectorySeparatorChar

    /// Strips non-printable (control) characters from the string
    let inline stripControls (s : string) = s |> Seq.filter (fun c -> not (Char.IsControl(c))) |> String.Concat

    let takeUntil (c:char) (str:string) =
        match str.IndexOf c with
        | -1    -> str
        | num   -> str.Substring(0,num)

    let inline private min3(a,b,c) = min a (min b c)
    let inline private distanceCalc (m, u) (n, v) =
        let d1 = Array.init n id
        let d0 = Array.create n 0
        for i=1 to m-1 do
            d0.[0] <- i
            let ui = u i
            for j=1 to n-1 do
                d0.[j] <- 1 + min3(d1.[j], d0.[j-1], d1.[j-1] + if ui = v j then -1 else 0)
            Array.blit d0 0 d1 0 n
        d0.[n-1]

    let editDistance (s: string) (t: string) =
        distanceCalc (s.Length, fun i -> s.[i]) (t.Length, fun i -> t.[i])

// Process Helpers
//=====================================================

let prompt text =
    printfn text
    Console.Write "> "
    Console.ReadLine () |> String.stripControls

let promptSelect text list =
    printfn text
    list |> Seq.iter (printfn " - %s")
    printfn ""
    Console.Write "> "
    Console.ReadLine () |> String.stripControls

let promptSelect2 text list =
     printfn text
     list |> Array.iter (fun (n, v) -> printfn " - %s (%s)" n v)
     printfn ""
     Console.Write("> ")
     Console.ReadLine () |> String.stripControls

/// Loads the given text into a XmlDocument
let XMLDoc text =
    if String.isNullOrEmpty text then null else
    let xmlDocument = XmlDocument ()
    xmlDocument.LoadXml text
    xmlDocument


// Environment Config
//====================================================

let exeLocation       = System.Reflection.Assembly.GetEntryAssembly().Location |> Path.GetDirectoryName
let templatesLocation = exeLocation </> ".." </> "templates"
let directory         = System.Environment.CurrentDirectory
let packagesDirectory = directory </> "packages"

let paketLocation     = exeLocation </> "Tools" </> "Paket"
let fakeLocation      = exeLocation </> "Tools" </> "FAKE"
let fakeToolLocation  = fakeLocation </> "tools"

let filesLocation = templatesLocation </> ".files"
let templateFile = templatesLocation </> "templates.json"



let inline mapOpt (opt:'a option) mapfn (x:'b) =
    match opt with
    | None -> x
    | Some a -> mapfn a x

let parseGuid text =
    let mutable g = Unchecked.defaultof<Guid>
    if Guid.TryParse(text,&g) then Some g else None

let parseBool text =
    let mutable b = Unchecked.defaultof<bool>
    if Boolean.TryParse(text,&b) then Some b else None

[<RequireQualifiedAccess>]
module Option =

    /// Gets the value associated with the option or the supplied default value.
    let inline getOrElse v = function Some x -> x | None -> v

    /// Gets the value associated with the option or the supplied default value.
    let inline mapOrDefault mapfn v =
        function
        | Some x -> mapfn x
        | None -> v

[<RequireQualifiedAccess>]
module Dict =
    open System.Collections.Generic

    let add key value (dict: Dictionary<_,_>) =
        dict.[key] <- value
        dict

    let remove (key: 'k) (dict: Dictionary<'k,_>) =
        dict.Remove key |> ignore
        dict

    let tryFind key (dict: Dictionary<'k, 'v>) =
        let mutable value = Unchecked.defaultof<_>
        if dict.TryGetValue (key, &value) then Some value
        else None

    let ofSeq (xs: ('k * 'v) seq) =
        let dict = Dictionary()
        for k, v in xs do dict.[k] <- v
        dict


// COMPUTATION EXPRESSIONS
//=====================================

type MaybeBuilder () =
    [<DebuggerStepThrough>]
    member inline __.Return value: 'T option = Some value

    [<DebuggerStepThrough>]
    member inline __.ReturnFrom value: 'T option = value

    [<DebuggerStepThrough>]
    member inline __.Zero (): unit option = Some()   

    [<DebuggerStepThrough>]
    member __.Delay (f: unit -> 'T option): 'T option = f ()

    [<DebuggerStepThrough>]
    member inline __.Combine (r1, r2: 'T option): 'T option =
        match r1 with
        | None -> None
        | Some () -> r2

    [<DebuggerStepThrough>]
    member inline __.Bind (value, f: 'T -> 'U option): 'U option = Option.bind f value

    [<DebuggerStepThrough>]
    member __.Using (resource: ('T :> System.IDisposable), body: _ -> _ option): _ option =
        try body resource
        finally
            if not <| obj.ReferenceEquals (null, box resource) then
                resource.Dispose ()

    [<DebuggerStepThrough>]
    member x.While (guard, body: _ option): _ option =
        if not ^ guard () then None else
            // OPTIMIZE: This could be simplified so we don't need to make calls to Bind and While.
            x.Bind (body, (fun () -> x.While (guard, body)))

    [<DebuggerStepThrough>]
    member x.For (sequence: seq<_>, body: 'T -> unit option): _ option =
        // OPTIMIZE: This could be simplified so we don't need to make calls to Using, While, Delay.
        x.Using (sequence.GetEnumerator (), fun enum ->
            x.While (
                enum.MoveNext,
                x.Delay (fun () ->
                    body enum.Current)))


let maybe = MaybeBuilder()


// ACTIVE PATTERNS
//=====================================

let (|InvariantEqual|_|) (str:string) arg =
  if String.Compare(str, arg, StringComparison.OrdinalIgnoreCase) = 0
  then Some () else None
  
  
// Seq extension
//====================================

module Seq = 
    let duplicates xs =
        (Map.empty, xs)
        ||> Seq.scan (fun xs x ->
            match Map.tryFind x xs with
            | None -> Map.add x false xs
            | Some false -> Map.add x true xs
            | Some true -> xs)
        |> Seq.zip xs
        |> Seq.choose (fun (x, xs) ->
            match Map.tryFind x xs with
            | Some false -> Some x
            | None | Some true -> None)

let relative (path1 : string) (path2 : string) =
    let p1, p2 = Uri path1, Uri path2
    Uri.UnescapeDataString(
        p2.MakeRelativeUri(p1)
            .ToString()
            .Replace('/', Path.DirectorySeparatorChar)
    )  
    
            
