﻿/// Helpers for console interaction: setting colours, parsing input into tokens etc.
module Terminal

open System
open Model

/// The starting console colour, before it is overriden by prompts, outputs and help for example.
let originalColour = ConsoleColor.Gray

/// Resets the interface to use the default font colour.
let defaultColour () = 
    Console.ForegroundColor <- originalColour

/// Sets the console foreground colour (font colour) to the colour specified by the given string,
/// e.g. colour "Red" will set the foreground to ConsoleColor.Red.
/// String parsing is only used because its more concise than using the built in enum accessor.
let colour s = 
    let consoleColour = Enum.Parse (typeof<ConsoleColor>, s, true) :?> ConsoleColor
    Console.ForegroundColor <- consoleColour

/// Controls cursor visibility. 
/// The cursor should only be visible when accepting input from the user, and not when drawing the prompt, for example.
let cursor b = Console.CursorVisible <- b

/// Splits up a string into tokens, accounting for escaped spaces and quote/code wrapping,
/// e.g. '"hello" world "this is a" test\ test' would be processed as ["hello";"world";"this is a";"test test"].
let parts s =
    // The internal recursive function processes the input one char at a time via a list computation expression. 
    // This affords a good deal of control over the output, and is functional/immutable.
    let rec parts soFar wrapped last remainder = 
        [
            if remainder = "" then yield soFar
            else
                let c, next = remainder.[0], remainder.[1..]
                match c, wrapped with
                | '(', None when soFar = "" ->
                    yield! parts soFar (Some '(') last next
                | ')', Some '(' when last <> '\\' ->
                    yield sprintf "(%s)" soFar
                    yield! parts "" None last next
                | '\"', None when soFar = "" -> 
                    yield! parts soFar (Some '\"') last next
                | '\"', Some '\"' when last <> '\\' ->
                    yield sprintf "\"%s\"" soFar
                    yield! parts "" None last next
                | ' ', None when last <> '\\' ->
                    if soFar <> "" then yield soFar
                    yield! parts "" None last next
                | _ -> 
                    yield! parts (soFar + string c) wrapped c next
        ]
    parts "" None ' ' s

let tokens partlist = 
    [
        let mutable i = 0
        let max = List.length partlist - 1
        while i <= max do
            let part = partlist.[i]
            if part = "" then
                i <- i + 1
            elif part = "|>" && i <> 0 && i < max then 
                yield Pipe
                i <- i + 1
            elif part = ">>" && i < max then
                yield Out partlist.[i + 1]
                i <- i + 2
            elif part.[0] = '(' && (i = max || part.[part.Length - 1] = ')') then
                yield Code part
                i <- i + 1
            else
                let command = part
                let mutable ni = i
                while ni <= max + 1 do
                    if ni > max || partlist.[ni] = "|>" || partlist.[ni] = ">>" then
                        yield Command (command, partlist.[i+1..ni-1])
                    else
                        ni <- ni + 1
                i <- ni
    ]
    // read each part
    // if code or pipe return as such
    // if out return it plus next as out
    // else return command until pipe or out

/// Writes out a list of tokens to the output, coloured appropriately.
let writeTokens = 
    List.iter (function 
    | Command (s, args) -> 
        colour "Yellow"
        printf "%s " s
        defaultColour ()
        args |> List.iter (printf "%s ")
    | Code s ->
        colour "Cyan"
        printf "%s " s
    | Pipe ->
        colour "Green"
        printf "|> "
    | Out s ->
        colour "Green"
        printf ">> "
        defaultColour ()
        printf "%s" s)