// Learn more about F# at http://fsharp.org
namespace TwitterAPI
open System
open System.Net
open System.Collections.Generic

open Akka.Actor
open Akka.FSharp

open Suave
open Suave.Http
open Suave.Operators
open Suave.Filters
open Suave.Successful
open Suave.Files
open Suave.RequestErrors
open Suave.Logging
open Suave.Utils
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket

open Newtonsoft.Json
// open Newtonsoft.Json.Linq

open Common
open TwitterEngine

module WS =

    let userActor engineRef (wsoc:WebSocket) (mailbox:Actor<_>) = 
        let mutable feed = ""
        let rec loop () =
            actor{
                let! message = mailbox.Receive()
                match message with
                | LOGIN_REQUEST(userID, password) ->
                    let req:Async<_> = engineRef <? LOGIN(userID , password)
                    let res = Async.RunSynchronously req
                    match res with
                    | LOGGEDIN ->
                        let mutable ress = []
                        ress <- {response = "UserLogged"; data = userID} :: ress
                        mailbox.Sender() <! ress
                    | ERROR(errorMsg) -> 
                        let ress = [{response = "ERROR" ; data = errorMsg}]
                        mailbox.Sender() <! ress
                    | _ -> printfn "message received: %A" message
                | REGISTER_REQUEST(userID, password) ->
                    let req:Async<_> = engineRef <? REGISTER(userID, password)
                    printfn "response sent "
                    let res = Async.RunSynchronously req
                    printfn "response sent "
                    match res with
                    | REGISTERED ->
                        let ress = [{response = "UserRegistered";data = userID}]
                        mailbox.Sender() <! ress
                    | ERROR(errorMsg) -> 
                        let ress = [{response = "ERROR" ; data = errorMsg}]
                        mailbox.Sender() <! ress
                    | _ -> printfn "invalid message: %A" message
                | LOGOUT_REQUEST(userID) ->
                    let req:Async<_> = engineRef <? LOGOUT(userID)
                    let res = Async.RunSynchronously req
                    match res with
                    | LOGGEDOUT -> 
                        let ress = [{response = "LoggedOut";data = ""}]
                        mailbox.Sender() <! ress
                    | ERROR(errorMsg) -> 
                        let ress = [{response = "ERROR" ; data = errorMsg}]
                        mailbox.Sender() <! ress
                    | _ -> printfn "invalid message: %A" message
                | TWEET_REQUEST(u,tw,h,m) ->
                    let mutable t:tweet = new tweet() 
                    t.guID <- DateTime.Now.ToString() |> string
                    t.body <- sprintf "%s : %s" (DateTime.Now.ToString()) tw
                    t.hashtags <- h
                    t.mentions <- m
                    t.senderID <- u
                    engineRef <! TWEET(t)
                    let ress = [{response = "TweetSent"; data = ""}]
                    mailbox.Sender() <! ress
                | NEW_FEED(feedType, tweet) ->
                    let mutable s = ""
                    match feedType with
                    | MENTIONS_FEED ->
                        s <- s + tweet.senderID + " mentioned you in tweet :" + "\n\t" + tweet.body + "\n"
                    | SUBSCRIPTION_FEED ->
                        s <- s + tweet.senderID + " tweeted :" + "\n\t" + tweet.body  + "\n"
                    | HASHTAG_FEED ->
                        s <- s + " New tweet from hashtag " + tweet.hashtags.ToString() + "\n\t" + tweet.body  + "\n"
                    feed <- feed + s
                | REFRESH_REQUEST(userID) ->
                    let ress = [{response = "Tweet" ; data = feed}]
                    mailbox.Sender() <! ress
                    feed <- ""
                | USERSUB_REQUEST(userID, suser) ->
                    let mutable s:subscription = new subscription()
                    s.subTo <- suser
                    s.subType <- USER_SUB
                    engineRef <! SUBSCRIBE(userID, s)
                    let ress = [{response = "UserSubscribed" ; data = ""}]
                    mailbox.Sender() <! ress
                | HASHTAG_SUB_REQUEST(userID, sHashtag) ->
                    let mutable s:subscription = new subscription()
                    s.subTo <- sHashtag
                    s.subType <- HASHTAG_SUB
                    engineRef <! SUBSCRIBE(userID, s)
                    let ress = [{response = "UserSubscribed" ; data = ""}]
                    mailbox.Sender() <! ress
                | SEARCH_REQUEST(userID, queryType, query) ->
                    let mutable q:query = new query()
                    q.queryString <- query
                    match queryType with
                    | "user" ->
                        q.queryType <- SUBSCRIPTION
                    | "hashtag" ->
                        q.queryType <- HASHTAG
                    | _ -> printfn "invalid query type"
                    
                    let req:Async<_> = engineRef <? QUERY(userID, q)
                    let res = Async.RunSynchronously req
                    match res with
                    | QUERY_RESULT(query) -> 
                        let mutable result = ""
                        for t in query.result do
                            result <- result + t.senderID + " " + t.body + "\n"
                        let ress = [{response = "SearchResult";data = result}]
                        mailbox.Sender() <! ress
                | _ -> printfn "invalid message: %A" message
                return! loop ()
            }
        loop()




  

    let ws (webSocket : WebSocket) (context: HttpContext) =
        
        socket {
            let mutable loop = true
            while loop do
                let! msg = webSocket.read()

                match msg with
                // | (Open, _,_)->()
                | (Text, data, true) ->
                    let str = UTF8.toString data
                    let json = JsonConvert.DeserializeObject<JRequest>(str)
                    match json.request with
                    | "Login" ->
                        if users.ContainsKey json.username then
                            let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                            let req:Async<_> = node <? LOGIN_REQUEST(json.username, json.password)
                            let res = Async.RunSynchronously req
                            for r in res do
                                let jres = JsonConvert.SerializeObject(r)
                                let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                                do! webSocket.send Text bres true
                    | "Register" ->
                        if users.ContainsKey  json.username then
                            let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                            let req:Async<_> = node <? REGISTER_REQUEST(json.username, json.password)
                            let res = Async.RunSynchronously req
                            for r in res do
                                let jres = JsonConvert.SerializeObject(r)
                                let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                                do! webSocket.send Text bres true
                        else
                            let name = "user-" + json.username 
                            let node = spawn serversystem name <| userActor engineRef webSocket
                            let req:Async<_> = node <? REGISTER_REQUEST(json.username, json.password)
                            let res = Async.RunSynchronously req
                            for r in res do
                                let jres = JsonConvert.SerializeObject(r)
                                let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                                do! webSocket.send Text bres true
                    | "Logout" ->
                        let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                        let req:Async<_> = node <? LOGOUT_REQUEST(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Tweet" ->
                        let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                        let req:Async<_> = node <? TWEET_REQUEST(json.username,json.tweet,json.hashtags ,json.mentions)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Refresh" ->
                        let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                        let req:Async<_> = node <? REFRESH_REQUEST(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Subscribe" ->
                        let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                        match json.subtype with
                        | "user" ->
                            let req:Async<_> = node <? USERSUB_REQUEST(json.username, json.subTo)
                            let res = Async.RunSynchronously req
                            for r in res do
                                let jres = JsonConvert.SerializeObject(r)
                                let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                                do! webSocket.send Text bres true
                        | "hashtag" ->
                            let req:Async<_> = node <? HASHTAG_SUB_REQUEST(json.username, json.subTo)
                            let res = Async.RunSynchronously req
                            for r in res do
                                let jres = JsonConvert.SerializeObject(r)
                                let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                                do! webSocket.send Text bres true   
                        | _ -> printfn "Unknown request"
                    | "Search" ->
                            let node = select ("akka.tcp://Server@localhost:9002/user/user-" + json.username) serversystem
                            let req:Async<_> = node <? SEARCH_REQUEST(json.username, json.searchType, json.search)
                            let res = Async.RunSynchronously req
                            for r in res do
                                let jres = JsonConvert.SerializeObject(r)
                                let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                                do! webSocket.send Text bres true
                    | _ -> printfn "Unknown request"

                | (Close, _, _) ->
                    let emptyResponse = [||] |> ByteSegment
                    do! webSocket.send Close emptyResponse true
                    loop <- false

                | _ -> printfn "%A" msg
        }

    let app : WebPart =
        choose [
          path "/websocket" >=> handShake ws
          GET >=> choose [ path "/" >=> file "./Client/index.html"; browseHome ]
          GET >=> path "/styles.css" >=> file "./Client/styles.css" 
          GET >=> path "/bootstrap.min.css" >=> file "./Client/bootstrap.min.css"
          GET >=> path "/jquery.min.js" >=> file "./Client/jquery.min.js" 
          GET >=> path "/bootstrap.min.js" >=> file "./Client/bootstrap.min.js" 
          GET >=> path "/app.js" >=> file "./Client/app.js" 
          NOT_FOUND "Found no handlers." ]

    [<EntryPoint>]
    let main argv =
        // printfn "Hello World from F#!"
        startWebServer defaultConfig app
        0 // return an integer exit code
