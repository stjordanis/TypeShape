﻿module TypeShape.HKT.PrettyPrinter

open System
open System.Text

#nowarn "20"

// HKT encoding for prettyprinter types
type PrettyPrinter =
    static member Assign(_ : App<PrettyPrinter, 'a>, _ : Action<StringBuilder, 'a>) = ()

[<AutoOpen>]
module private Helpers =

    type Act<'a> = Action<StringBuilder, 'a>

    let inline append (sb : StringBuilder) (value : string) = sb.Append(value) |> ignore
    let inline appendSeq (sb : StringBuilder) xs (f : Action<StringBuilder, 'a>) =
        let mutable first = true
        for x in xs do
            if first then first <- false else sb.Append "; " |> ignore
            f.Invoke(sb, x)

type PrettyPrinterBuilder() =
    interface ITypeBuilder<PrettyPrinter, PrettyPrinter> with
        member __.Bool () = HKT.pack(Act(fun s b -> append s (if b then "true" else "false")))
        member __.Byte () = HKT.pack(Act(fun s b -> s.Append(b).Append("uy") |> ignore))
        member __.SByte() = HKT.pack(Act(fun s b -> s.Append(b).Append("y") |> ignore))
        member __.Char () = HKT.pack(Act(fun s c -> s.Append('\'').Append(c).Append('\'') |> ignore))
        
        member __.Int16 () = HKT.pack(Act(fun s i -> s.Append(i).Append('s') |> ignore))
        member __.Int32 () = HKT.pack(Act(fun s i -> s.Append(i) |> ignore))
        member __.Int64 () = HKT.pack(Act(fun s i -> s.Append(i).Append('L') |> ignore))

        member __.UInt16 () = HKT.pack(Act(fun s i -> s.Append(i).Append("us") |> ignore))
        member __.UInt32 () = HKT.pack(Act(fun s i -> s.Append(i).Append('u' ) |> ignore))
        member __.UInt64 () = HKT.pack(Act(fun s i -> s.Append(i).Append("uL") |> ignore))

        member __.Single () = HKT.pack(Act(fun s f -> s.Append(f).Append('f') |> ignore))
        member __.Double () = HKT.pack(Act(fun s f -> s.Append(f) |> ignore))
        member __.Decimal() = HKT.pack(Act(fun s f -> s.Append(f).Append('M') |> ignore))
        member __.BigInt () = HKT.pack(Act(fun s f -> s.Append(f).Append('I') |> ignore))

        member __.Unit() = HKT.pack(Act(fun s () -> append s "()"))
        member __.String () = HKT.pack(Act(fun s str -> s.Append('"').Append(str).Append('"') |> ignore))
        member __.Guid () = HKT.pack(Act(fun s g -> s.Append(g) |> ignore))

        member __.TimeSpan () = HKT.pack(Act(fun s t -> s.Append(t) |> ignore))
        member __.DateTime () = HKT.pack(Act(fun s d -> s.Append(d) |> ignore))
        member __.DateTimeOffset() = HKT.pack(Act(fun s d -> s.Append(d) |> ignore))

        member __.Nullable (HKT.Unpack ep) = HKT.pack(Act(fun s x -> if x.HasValue then ep.Invoke(s, x.Value) else append s "null"))
        member __.Enum _ = HKT.pack(Act(fun s e -> s.Append e |> ignore))
        member __.Array (HKT.Unpack ep) = HKT.pack(Act(fun s xs -> append s "[|" ; appendSeq s xs ep ; append s "|]"))

        member __.Option (HKT.Unpack ep) = HKT.pack(Act(fun s x -> match x with None -> append s "None" | Some x -> append s "Some(" ; ep.Invoke(s, x) ; append s ")"))
        member __.List (HKT.Unpack ep) = HKT.pack(Act(fun s xs -> append s "[" ; appendSeq s xs ep ; append s "]"))
        member __.Set (HKT.Unpack ep) = HKT.pack(Act(fun s xs -> append s "set [" ; appendSeq s xs ep ; append s "]"))
        member __.Map (HKT.Unpack kp) (HKT.Unpack vp) = 
            HKT.pack(Act(fun s xs -> 
                let mutable first = true
                append s "map ["
                for kv in xs do
                    if first then first <- false else append s "; "

                    append s "("
                    kp.Invoke(s, kv.Key)
                    append s ", "
                    vp.Invoke(s, kv.Value)
                    append s ")"
                append s "]"))

        member __.Field shape (HKT.Unpack fp) = HKT.pack(Act(fun s t -> fp.Invoke(s, shape.Get t)))

        member __.Tuple shape (HKT.Unpacks fields) =
            let isStruct = shape.IsStructTuple
            HKT.pack(Act(fun s tuple ->
                if isStruct then append s "struct(" else append s "("
                let mutable isFirst = true
                for f in fields do
                    if isFirst then isFirst <- false else append s ", "
                    f.Invoke(s, tuple)
                append s ")"))

        member __.Record shape (HKT.Unpacks fields) =
            let isAnon = shape.IsAnonymousRecord
            let isStruct = shape.IsStructRecord
            let fieldLabels = 
                shape.Fields 
                |> Array.map (fun f -> f.Label)
                |> Array.zip fields

            HKT.pack(Act(fun s record ->
                if isAnon then 
                    if isStruct 
                    then append s "struct {|"
                    else append s "{|"
                else append s "{"

                let mutable isFirst = true
                for field,label in fieldLabels  do
                    if isFirst then isFirst <- false else append s "; "
                    append s label
                    append s " = "
                    field.Invoke(s, record)
               
                if isAnon then append s "|}" else append s "}"))

        member __.Union shape (HKT.Unpackss fieldss) =
            HKT.pack(Act(fun s union ->
                let tag = shape.GetTag union
                let case = shape.UnionCases.[tag]
                append s case.CaseInfo.Name
                if case.Fields.Length > 0 then
                    append s "("
                    let mutable first = true
                    for f in fieldss.[tag] do
                        if first then first <- false else append s ", "
                        f.Invoke(s, union)
                    append s ")"))

        member __.CliMutable shape (HKT.Unpacks fields) =
            let name = shape.DefaultCtorInfo.DeclaringType.Name
            let fieldProps =
                shape.Properties
                |> Array.map (fun p -> p.Label)
                |> Array.zip fields

            HKT.pack(Act(fun s record ->
                append s name
                append s "("
                let mutable first = true
                for f, label in fieldProps do
                    if first then first <- false else append s ", "
                    append s label
                    append s " = "
                    f.Invoke(s, record)
                append s ")"))

        member __.Delay f = HKT.pack(Act(fun s x -> (HKT.unpack f.Value).Invoke(s, x)))

let mkPrinter<'t> () : 't -> string = 
    let action = TypeBuilder.fold (PrettyPrinterBuilder()) |> HKT.unpack
    fun t ->
        let sb = new StringBuilder()
        action.Invoke(sb, t)
        sb.ToString()

//----------------------

type P = Z | S of P

let p = mkPrinter<int * (string * DateTime) option * {| x : int ; p : P |}>()

p (42, Some ("42", DateTime.Now), {| x = 42 ; p = S(S(S Z)) |})