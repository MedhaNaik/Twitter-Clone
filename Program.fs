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

open Typedef
open Engine

module WS =

    let clientNode eng (wsoc:WebSocket) (mailbox:Actor<_>) = 
        let mutable user:User = User()
        let mutable subscribers:List<User> = new List<User>()
        let mutable tweetList:List<TweetMessage> = new List<TweetMessage>()
        let rec loop () =
            actor{
                let! message = mailbox.Receive()
                match message with
                | CLoginUser(username) ->
                    if user.ID = -1 then
                        let req:Async<_> = eng <? LoginUser(username,mailbox.Self)
                        let res = Async.RunSynchronously req
                        match res with
                        | UserLogged(u,subs,tweets) ->
                            user <- u
                            subscribers <- subs
                            if user.ID <> 1 then subscribers.Add(User(1,"user1"));
                            let mutable ress = []
                            ress <- {response = "UserLogged"; data = u.Username} :: ress
                            for tweet in tweets do
                                tweetList.Add(tweet)
                                let mutable s = ""
                                if tweet.retweet then s <- s + tweet.user.Username + " retweeted "
                                else s <- s + tweet.user.Username + " tweeted "
                                s <- s + tweet.tweet.Value
                                if tweet.mention then s <- s + " mentioned you"
                                ress <- {response = "Tweet";data = s } :: ress
                            mailbox.Sender() <! ress
                        | UserNotFound -> 
                            let ress = [{response = "UserNotFound";data = ""}]
                            mailbox.Sender() <! ress
                            // do! wsoc.send Text bres true
                        | _ -> printfn "message received: %A" message
                    else 
                        let ress = [{response = "LoggedOut";data = ""}]
                        user <- User()
                        mailbox.Sender() <! ress
                | CRegisterUser(username) ->
                    if user.ID = -1 then
                        let req:Async<_> = eng <? RegisterUser(username, mailbox.Self)
                        let res = Async.RunSynchronously req
                        match res with
                        | UserRegistered(u) ->
                            user <- u
                            let ress = [{response = "UserRegistered";data = username}]
                            mailbox.Sender() <! ress
                        | UserExists(u) ->
                            user <- u
                            let ress = [{response = "UserExists";data = username}]
                            mailbox.Sender() <! ress
                    else 
                        let ress = [{response = "LoggedOut";data = ""}]
                        user <- User()
                        mailbox.Sender() <! ress
                | CTweetRequest(u,t,h,m) ->
                    let req:Async<_> = eng <? TweetRequest(user,t,h,m,subscribers)
                    let res = Async.RunSynchronously req 
                    let ress = [{response = "TweetSent";data = ""}]
                    mailbox.Sender() <! ress
                | CRefresh(username) -> 
                    let mutable ress = []
                    if tweetList.Count <> 0 then
                        for tweet in tweetList do
                            let mutable s = ""
                            if tweet.retweet then s <- s + tweet.user.Username + " retweeted "
                            else s <- s + tweet.user.Username + " tweeted "
                            s <- s + tweet.tweet.Value
                            if tweet.mention then s <- s + " mentioned you"
                            ress <- {response = "Tweet";data = s } :: ress
                        tweetList.Clear()
                    mailbox.Sender() <! ress
                | CTweetUpdate(t,u,r) ->
                        let tadd:TweetMessage = new TweetMessage(t,u,r,false)
                        tweetList.Add(tadd)
                        // mailbox.Sender() <! TweetReceived
                | CSubscribeUser(suser) ->
                    let req:Async<_> = eng <? SubscribeUserA(user,suser)
                    let res = Async.RunSynchronously req
                    match res with 
                    | _ -> ()
                    let ress = [{response = "UserSubscribed";data = ""}]
                    mailbox.Sender() <! ress
                | CAddSubscriber(s)->
                    subscribers.Add(s)
                | CLogoutUser(username) ->
                    if user.ID <> -1 then
                        let req:Async<_> = eng <? LogoutUser(user)
                        let res = Async.RunSynchronously req
                        match res with
                        | UserLoggedOut ->
                            let ress = [{response = "LoggedOut";data = ""}]
                            user <- User()
                            mailbox.Sender() <! ress
                    else
                        let ress = [{response = "LoggedOut";data = ""}]
                        user <- User()
                        subscribers <- new List<User>()
                        tweetList <- new List<TweetMessage>()
                        mailbox.Sender() <! ress
                | _ -> printfn "message received: %A" message
                return! loop ()
            }
        loop()

    Engine.twitsetdb(10)
    let twitterEngine = spawn rs "twitter-engine" engine

    let ws (webSocket : WebSocket) (context: HttpContext) =
        let mutable u = User()
        let nodeName = "cnode" + string(webSocket.GetHashCode())
        let node = spawn rs nodeName <| clientNode twitterEngine webSocket
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
                        let req:Async<_> = node <? CLoginUser(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Register" ->
                        let req:Async<_> = node <? CRegisterUser(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Logout" ->
                        let req:Async<_> = node <? CLogoutUser(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Tweet" ->
                        let req:Async<_> = node <? CTweetRequest(json.username,json.tweet,json.hashtags,json.mentions)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Refresh" ->
                        let req:Async<_> = node <? CRefresh(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | "Subscribe" ->
                        let req:Async<_> = node <? CSubscribeUser(json.username)
                        let res = Async.RunSynchronously req
                        for r in res do
                            let jres = JsonConvert.SerializeObject(r)
                            let bres = jres |> System.Text.Encoding.ASCII.GetBytes |> ByteSegment
                            do! webSocket.send Text bres true
                    | _ -> printfn "Unknown request"
                    // let response = sprintf "response to %s" str
                    // let byteResponse =
                    //     response
                    //     |> System.Text.Encoding.ASCII.GetBytes
                    //     |> ByteSegment
                    // do! webSocket.send Text byteResponse true

                | (Close, _, _) ->
                    let emptyResponse = [||] |> ByteSegment
                    do! webSocket.send Close emptyResponse true
                    loop <- false

                | _ -> printfn "%A" msg
        }

    let app : WebPart =
        choose [
          path "/websocket" >=> handShake ws
          GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
          GET >=> path "/styles.css" >=> file "styles.css" 
          GET >=> path "/bootstrap.min.css" >=> file "bootstrap.min.css"
          GET >=> path "/jquery.min.js" >=> file "jquery.min.js" 
          GET >=> path "/bootstrap.min.js" >=> file "bootstrap.min.js" 
          GET >=> path "/app.js" >=> file "app.js" 
          NOT_FOUND "Found no handlers." ]

    [<EntryPoint>]
    let main argv =
        // printfn "Hello World from F#!"
        startWebServer defaultConfig app
        0 // return an integer exit code
