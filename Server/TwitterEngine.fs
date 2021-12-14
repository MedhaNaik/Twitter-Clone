
namespace TwitterAPI
open System
open Akka.FSharp
open Akka.Remote
open Akka.Configuration
open System.Collections.Generic
open Common


module TwitterEngine =

    let serversystem = System.create "Server" configuration
    let mutable users: Map<string, string> = Map.empty
    let mutable connected: Set<string> = Set.empty
    // tweet id and tweets
    let mutable tweetMap: Map<string, tweet> = Map.empty

    // actor that send response to remote users
    let response (mailbox:Actor<_>) =
        let rec loop () = actor {
            let! (message:ResponseMessage) = mailbox.Receive()
            match message with
            | SEND_REGISTERED(userActor) -> 
                // create new endpoint actor with given id and add to the list
                //let userActor = select ("akka.tcp://Server@localhost:9002/user/user-" + userID) serversystem
                userActor <! REGISTERED
                printfn "registered response"
            | SEND_LOGGEDIN(userActor) -> 
                // create new endpoint actor with given id and add to the list
                //let userActor = select ("akka.tcp://Server@localhost:9002/user/user-" + userID) serversystem
                userActor <! LOGGEDIN
                printfn "Logged in response"
            | SEND_LOGGEDOUT(userActor) -> 
                // create new endpoint actor with given id and add to the list
                //let userActor = select ("akka.tcp://Server@localhost:9002/user/user-" + userID) serversystem
                userActor <! LOGGEDOUT
                printfn "Logged out response"
            | SEND_ERROR(userActor, errorMsg) -> 
                // create new endpoint actor with given id and add to the list
                //let userActor = select ("akka.tcp://Server@localhost:9002/user/user-" + userID) serversystem
                userActor <! ERROR(errorMsg)
                printfn "ERROR response: %s" errorMsg
            | SEND_FEED(userID, newTweet, feedType) ->
                let userActor = select ("akka.tcp://Server@localhost:9002/user/user-" + userID) serversystem
                userActor <! NEW_FEED(feedType, newTweet)
                //printfn "tweet for %s : %s" userID newTweet.body
            | SEND_RESULT(userID, query) ->
                //printfn "query: %s " query.queryString
                let userActor = select ("akka.tcp://Server@localhost:9002/user/user-" + userID) serversystem
                userActor <! QUERY_RESULT(query)
            | _ -> printfn "Server Received Wrong message"
            return! loop()
        }
        loop ()




    // user subscription manager
    let subscriber_manager (mailbox:Actor<_>) =
        let responseActor = select ("akka.tcp://Server@localhost:9002/user/response") serversystem
        // user ids and tweets
        let mutable userTweetMap: Map<string, Set<string>> = Map.empty
        // user id and subsciber ids
        let mutable userSubscriberMap: Map<string, Set<string>> = Map.empty

        let rec loop () = actor {
            let! (message:FeedMessage) = mailbox.Receive()
            match message with
            | FEED(userID, newTweet) ->
                let tweetID = newTweet.guID
                // add tweet to the user tweet map
                if userTweetMap.ContainsKey(userID) then
                    userTweetMap <- userTweetMap.Add(userID,  userTweetMap.[userID].Add(tweetID))
                else
                    userTweetMap <- Map.add userID Set.empty userTweetMap
                    userTweetMap <- userTweetMap.Add(userID,  userTweetMap.[userID].Add(tweetID))
                
                // send tweet to all subscribers
                if userSubscriberMap.ContainsKey userID then
                    Set.map (fun id -> (if connected.Contains id then responseActor <! SEND_FEED(id, newTweet, SUBSCRIPTION_FEED))) userSubscriberMap.[userID] |> ignore

            | NEWSUB(userID, subscription) ->
                let user = subscription.subTo
                if userSubscriberMap.ContainsKey(user) then
                    userSubscriberMap <- userSubscriberMap.Add(user, userSubscriberMap.[user].Add(userID))
                else
                    userSubscriberMap <- Map.add user Set.empty userSubscriberMap
                    userSubscriberMap <- userSubscriberMap.Add(user, userSubscriberMap.[user].Add(userID))

            | NEWQUERY(userID, query) ->
                let result = new List<tweet>()
                if userTweetMap.ContainsKey query.queryString then
                    Set.map (fun tweetID -> result.Add(tweetMap.[tweetID])) userTweetMap.[query.queryString] |> ignore
                    query.result <- result
                    responseActor <! SEND_RESULT(userID, query)
            | _ -> printfn "Server Received Wrong message"
            return! loop()
        }
        loop ()

    // mentions manager
    let mentions_manager (mailbox:Actor<_>) =
        let responseActor = select ("akka.tcp://Server@localhost:9002/user/response") serversystem
        // user id and tweet ids with mentions
        let mutable mentionsMap: Map<string, Set<string>> = Map.empty

        let rec loop () = actor {
            let! (message:FeedMessage) = mailbox.Receive()
            match message with
            | FEED(uid, newTweet) ->
                let tweetID = newTweet.guID
                let mentions = newTweet.mentions
                // add tweet to the user mentions map
                for userID in mentions do
                    if mentionsMap.ContainsKey(userID) then
                        mentionsMap <- mentionsMap.Add(userID,  mentionsMap.[userID].Add(tweetID))
                    else
                        mentionsMap <- Map.add userID Set.empty mentionsMap
                        mentionsMap <- mentionsMap.Add(userID,  mentionsMap.[userID].Add(tweetID))

                    if connected.Contains userID then
                        responseActor <! SEND_FEED(userID, newTweet, MENTIONS_FEED)
            | NEWQUERY(userID, query) ->
                let result = new List<tweet>()
                if mentionsMap.ContainsKey query.queryString then
                    Set.map (fun tweetID -> result.Add(tweetMap.[tweetID])) mentionsMap.[query.queryString] |> ignore
                    query.result <- result
                    responseActor <! SEND_RESULT(userID, query)
            | _ -> printfn "Server Received Wrong message"
            return! loop()
        }
        loop ()

    // hashtags manager
    let hashtags_manager (mailbox:Actor<_>) =
        let responseActor = select ("akka.tcp://Server@localhost:9002/user/response") serversystem
        // hashtag and tweet ids
        let mutable hashtagMap: Map<string, Set<string>> = Map.empty
        // hashtag and tweet ids
        let mutable hashtagSubscriberMap: Map<string, Set<string>> = Map.empty

        let rec loop () = actor {
            let! (message:FeedMessage) = mailbox.Receive()
            match message with
            | FEED(userID, newTweet) ->
                let tweetID = newTweet.guID
                let hashtags = newTweet.hashtags
                
                for hashtag in hashtags do
                    if hashtagMap.ContainsKey(hashtag) then
                        hashtagMap <- hashtagMap.Add(hashtag,  hashtagMap.[hashtag].Add(tweetID))
                    else
                        hashtagMap <- Map.add hashtag Set.empty hashtagMap
                        hashtagMap <- hashtagMap.Add(hashtag,  hashtagMap.[hashtag].Add(tweetID))
                
                    if hashtagSubscriberMap.ContainsKey hashtag then
                        Set.map (fun id -> (if connected.Contains id then responseActor <! SEND_FEED(id, newTweet, HASHTAG_FEED))) hashtagSubscriberMap.[hashtag] |> ignore
            | NEWSUB(userID, subscription) ->
                    let hashtag = subscription.subTo
                    if hashtagSubscriberMap.ContainsKey(hashtag) then
                        hashtagSubscriberMap <- hashtagSubscriberMap.Add(hashtag, hashtagSubscriberMap.[hashtag].Add(userID))
                    else
                    
                        hashtagSubscriberMap <- Map.add hashtag Set.empty hashtagSubscriberMap
                        hashtagSubscriberMap <- hashtagSubscriberMap.Add(hashtag, hashtagSubscriberMap.[hashtag].Add(userID))
            
            | NEWQUERY(userID, query) ->
                let result = new List<tweet>()
                if hashtagMap.ContainsKey query.queryString then
                    Set.map (fun tweetID -> result.Add(tweetMap.[tweetID])) hashtagMap.[query.queryString] |> ignore
                    query.result <- result
            
                    responseActor <! SEND_RESULT(userID, query)
            | _ -> printfn "Server Received Wrong message"
            return! loop()
        }
        loop ()


    // entrypoint receives all user messages and delegates to respective user actor after authentication
    let entrypoint (mailbox:Actor<_>) =
        let rec loop () = actor {
            let responseActor = select ("akka.tcp://Server@localhost:9002/user/response") serversystem
            let subscribersActor = select ("akka.tcp://Server@localhost:9002/user/subscribers") serversystem
            let mentionsActor = select ("akka.tcp://Server@localhost:9002/user/mentions") serversystem
            let hashtagsActor = select ("akka.tcp://Server@localhost:9002/user/hashtags") serversystem

            let! (message:ServerMessage) = mailbox.Receive()
            match message with
            | REGISTER(userID, password) -> 
                
                // make sure user id doesn't exist
                if users.ContainsKey userID then
                    responseActor <! SEND_ERROR(mailbox.Sender(), "User already exists")
                else
                    users <- Map.add userID password users
                    responseActor <! SEND_REGISTERED(mailbox.Sender())
                
            | LOGIN(userID, password) -> 
                // make sure user exists
                if users.ContainsKey userID then
                    if not (users.[userID].Equals(password)) then
                        responseActor <! SEND_ERROR(mailbox.Sender(), "Incorrect username/password")
                    else
                        connected <- connected.Add(userID)
                        responseActor <! SEND_LOGGEDIN(mailbox.Sender())
                else
                    responseActor <! SEND_ERROR(mailbox.Sender(), "User does not exist")
            | LOGOUT(userID) -> 
                if connected.Contains userID then
                    connected <- connected.Remove(userID)
                    responseActor <! SEND_LOGGEDOUT(mailbox.Sender())
                else
                    responseActor <! SEND_ERROR(mailbox.Sender(), "User not logged in")
            
            | TWEET(newTweet) -> 
                let id = newTweet.guID
                let userID = newTweet.senderID
                tweetMap <- Map.add id newTweet tweetMap
                //printfn "tweet: %s" newTweet.body
                // send this tweet to all subscribers
                subscribersActor <! FEED(userID, newTweet)
                // send mentions to those ids
                mentionsActor <! FEED(userID, newTweet)
                // send hashtags to hashtag subscribers
                hashtagsActor <! FEED(userID, newTweet)
            | RETWEET(userID, tweetID) -> 
                let newTweet = tweetMap.[tweetID]
                // send this tweet to all subscribers
                subscribersActor <! FEED(userID, newTweet)
            | QUERY(userID, newQuery) ->
                match newQuery.queryType with
                | MENTIONS ->
                    // return all tweets with mentions of user
                    mentionsActor <! NEWQUERY(userID, newQuery)
                | HASHTAG ->
                    hashtagsActor <! NEWQUERY(userID, newQuery)
                    // return all tweets with given hashtag
                | SUBSCRIPTION ->
                    subscribersActor <! NEWQUERY(userID, newQuery)
                    // return all tweets of a subscribed user
            | SUBSCRIBE(userID, newSub) ->
                match newSub.subType with
                | HASHTAG_SUB ->
                    hashtagsActor <! NEWSUB(userID, newSub)
                | USER_SUB ->
                    subscribersActor <! NEWSUB(userID, newSub)
            | _ -> printfn "Server Received Wrong message"
            return! loop()
        }
        loop ()

    
   
    let engineRef = spawn serversystem "server" entrypoint
    let responseRef = spawn serversystem "response" response
    let mentionsRef = spawn serversystem "mentions" mentions_manager
    let hashtagRef = spawn serversystem "hashtags" hashtags_manager
    let subscriberRef = spawn serversystem "subscribers" subscriber_manager
   

    //Console.ReadLine() |> ignore