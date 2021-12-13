namespace TwitterAPI

open System
open System.Collections.Generic
open System.Data
open Akka.FSharp
open Akka.Actor
open Typedef
open Database

module Engine =

    let rs = ActorSystem.Create("Twitter")

    let db = Database.db
    let twitsetdb(numUsers) =
        Database.makeDatabase (numUsers)

    let getTweetFromDB(id) =
        let ttable = db.Tables.["Tweets"]
        let utable = db.Tables.["Users"]
        let htable = db.Tables.["Hashtags"]
        let httable = db.Tables.["Hashtag-Tweet"]
        let mtable = db.Tables.["Mentions"]
        let exp = "id = '"+string(id)+"'"
        let rows = ttable.Select(exp)
        // if rows.Length > 0 then  // run function only when tweet is available; so before running function, use select function to check whether data exists or not
        let row = rows.[0]
        let hashtags = httable.Select("tweetid = '"+string(id)+"'") 
                        |> Array.toList
                        |> List.map(fun htrow ->
                            let hrow = htable.Select("id = '" + string(htrow.["hashtagid"]) + "'").[0]
                            Hashtag(int(string(hrow.["id"])),string(hrow.["hashtag"]))
                            )
        let mentions = mtable.Select("tweetid = '"+string(id)+"'")
                        |> Array.toList
                        |> List.map(fun mrow ->
                            let urow = utable.Select("id = '" + string(mrow.["userid"]) + "'").[0]
                            User(int(string(urow.["id"])),string(urow.["username"]))
                        )
        Tweet(id,string(row.["tweet"]),hashtags,mentions)

    let collectTweets (uid:int) =
        let exp = "subscriberid = '"+string(uid)+"'"
        let sids = db.Tables.["Subscribers"].Select(exp) |> Array.map(fun row -> row.["userid"]) |> Array.toList
        let utable = db.Tables.["Users"]
        let ttable = db.Tables.["Tweets"]
        let rtable = db.Tables.["Retweets"]
        let mtable = db.Tables.["Mentions"]
        let tids = HashSet<int>()
        let rids = HashSet<int>()
        let tweetmsglist:List<TweetMessage> = List<TweetMessage>()
        for sid in sids do
            let exp = "id = '"+string(sid)+"'"
            let dataRows = utable.Select(exp)
            let suser:User = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
            let exp = "userid = '"+string(sid)+"'"
            let dataRows = ttable.Select(exp)
            for row in dataRows do
                tids.Add(int(string(row.["id"]))) |> ignore
                let tweet:Tweet = getTweetFromDB(int(string(row.["id"])))
                let mentions = tweet.Mentions |> List.map (fun m -> m.ID)
                let mentioned:bool = List.contains uid mentions
                let tmsg:TweetMessage = TweetMessage(tweet,suser,false,mentioned)
                tweetmsglist.Add(tmsg)
            let dataRows = rtable.Select(exp)
            for row in dataRows do
                rids.Add(int(string(row.["tweetid"]))) |> ignore
                let tweet:Tweet = getTweetFromDB(int(string(row.["tweetid"])))
                let mentions = tweet.Mentions |> List.map (fun m -> m.ID)
                let mentioned:bool = List.contains uid mentions
                let tmsg:TweetMessage = TweetMessage(tweet,suser,true,mentioned)
                tweetmsglist.Add(tmsg)
        let exp = "userid = '"+string(uid)+"'"
        let mrows = mtable.Select(exp)
        for mrow in mrows do
            let tid = int(string(mrow.["tweetid"]))
            if tids.Contains(tid) = false then
                let tweet:Tweet = getTweetFromDB(tid)
                let usid = int(string(ttable.Select("id = '"+string(tweet.ID)+"'").[0].["userid"]))
                let urow = utable.Select("id = '"+string(usid)+"'").[0]
                let u = User(int(string(urow.["id"])),string(urow.["username"]))
                let tmsg:TweetMessage = TweetMessage(tweet,u,false,true)
                tweetmsglist.Add(tmsg)
        tweetmsglist

    let engine (mailbox: Actor<_>) =
        let mutable loggedInUsers = new Dictionary<int,IActorRef>()
        let mutable totalLogged = 0
        let mutable totalTweets = 0
        let mutable expectedToSend = 0
        let mutable actualToSend = 0 
        let rec loop () =
            actor {
                let! message = mailbox.Receive()

                match message with
                | LoginUser (username,mail) ->
                    let table = db.Tables.["Users"]
                    let expression = "username = '" + username + "'"
                    let dataRows = table.Select(expression)
                    if dataRows.Length = 0 then
                        mailbox.Sender() <! UserNotFound
                    // printfn "%A" UserNotFound
                    else
                        let id = Convert.ToInt32 dataRows.[0].["id"]

                        let un =
                            Convert.ToString dataRows.[0].["username"]

                        let user: User = User(id, un)
                        // let tweets = new List<TweetMessage>()
                        let subtable = db.Tables.["Subscribers"]
                        let subscribers = new List<User>()
                        let subRows = subtable.Select("userid = '"+string(user.ID)+"'")
                        for subRow in subRows do
                            let dataRows = db.Tables.["Users"].Select("id = '"+string(subRow.["subscriberid"])+"'")
                            if dataRows.Length > 0 then
                                let sub = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
                                subscribers.Add(sub)                        
                        loggedInUsers.Add(id,mail) |> ignore
                        let tweetList = collectTweets(id)
                        printfn "%A: Logged in. Got %A tweets." username tweetList.Count
                        totalLogged <- totalLogged + 1
                        mailbox.Sender() <! UserLogged(user,subscribers,tweetList)
                | LogoutUser (u) ->
                    if loggedInUsers.ContainsKey(u.ID) then
                        loggedInUsers.Remove(u.ID) |> ignore
                        printfn "%A: Logged Out." u.Username
                        mailbox.Sender() <! UserLoggedOut
                    else
                        mailbox.Sender() <! UserLoggedOut
                | RegisterUser(username, mail) ->
                    let table = db.Tables.["Users"]
                    let expression = "username = '" + username + "'"
                    let dataRows = table.Select(expression)
                    if dataRows.Length = 0 then
                        let row:DataRow = table.NewRow()
                        row.["username"] <- username
                        table.Rows.Add row
                        printfn "New User %A is registered" username
                        let u:User = User(int(string(row.["id"])),string(row.["username"]))
                        loggedInUsers.Add(u.ID,mail) |> ignore
                        mailbox.Sender() <! UserRegistered(u)
                    else
                        printfn "User already exists. Can't register"
                        let u:User = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
                        loggedInUsers.Add(u.ID,mail) |> ignore
                        mailbox.Sender() <! UserExists(u)
                // send list of subscribers, tweets, retweets
                | SubscribeUser (c, s) ->
                    if loggedInUsers.ContainsKey(c.ID) then
                        let table = db.Tables.["Users"]
                        let exp = "id = '" + string (s.ID) + "'"
                        let dataRows = table.Select(exp)
                        if dataRows.Length = 0 then
                            printfn "%A: Subscriber Not Found" c.Username
                            mailbox.Sender() <! CSubscriberNotFound
                        else
                            let table = db.Tables.["Subscribers"]

                            let exp =
                                "userid = '"
                                + string (c.ID)
                                + "' AND subscriberid = '"
                                + string (s.ID)
                                + "'"

                            let dataRows = table.Select(exp)
                            if dataRows.Length <> 0 then
                                printfn "%A: Already subscribed" c.Username
                                mailbox.Sender() <! CAlreadySubscribed
                            else
                                let row = table.NewRow()
                                row.["userid"] <- c.ID
                                row.["subscriberid"] <- s.ID
                                table.Rows.Add(row)
                                printfn "%A: %A is added" c.Username s.Username
                                if loggedInUsers.ContainsKey(s.ID) then
                                    loggedInUsers.[s.ID] <! CAddSubscriber(c)
                                mailbox.Sender() <! CUserSubscribed
                    else
                        mailbox.Sender() <! NotAuthorised
                | SubscribeUserA (c,sub) ->
                    if loggedInUsers.ContainsKey(c.ID) then 
                        let table = db.Tables.["Users"]
                        let expression = "username = '" + sub + "'"
                        let dataRows = table.Select(expression)
                        if dataRows.Length = 0 then
                            printfn "%A: Subscriber Not Found" c.Username
                            mailbox.Sender() <! CSubscriberNotFound
                        else
                            let s:User = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
                            let table = db.Tables.["Subscribers"]
                            let exp =
                                "userid = '"
                                + string (c.ID)
                                + "' AND subscriberid = '"
                                + string (s.ID)
                                + "'"

                            let dataRows = table.Select(exp)
                            if dataRows.Length <> 0 then
                                printfn "%A: Already subscribed" c.Username
                                mailbox.Sender() <! CAlreadySubscribed
                            else
                                let row = table.NewRow()
                                row.["userid"] <- c.ID
                                row.["subscriberid"] <- s.ID
                                table.Rows.Add(row)
                                printfn "%A: %A is added" c.Username s.Username
                                if loggedInUsers.ContainsKey(s.ID) then
                                    loggedInUsers.[s.ID] <! CAddSubscriber(c)
                                mailbox.Sender() <! CUserSubscribed
                    else
                        mailbox.Sender() <! NotAuthorised       
                | TweetRequest (c, tweet, hashtags, mentions, subs) ->
                    if loggedInUsers.ContainsKey(c.ID) then
                        let table = db.Tables.["Tweets"]
                        let tweetrow = table.NewRow()
                        tweetrow.["tweet"] <- tweet
                        tweetrow.["userid"] <- c.ID
                        table.Rows.Add tweetrow
                        let mutable tweetHashtags = []
                        let mutable tweetMentions = []
                        let htable = db.Tables.["Hashtags"]
                        let httable = db.Tables.["Hashtag-Tweet"]
                        for hashtag in hashtags do
                            let exp = "hashtag = '"+hashtag+"'"
                            let dataRows = htable.Select(exp)
                            if dataRows.Length = 0 then
                                let row = htable.NewRow()
                                row.["hashtag"] <- hashtag
                                htable.Rows.Add row
                            let dataRows = htable.Select(exp)
                            let h = Hashtag(int(string(dataRows.[0].["id"])),string(dataRows.[0].["hashtag"]))
                            tweetHashtags <- h :: tweetHashtags
                            let row = httable.NewRow()
                            row.["hashtagid"] <- h.ID
                            row.["tweetid"] <- tweetrow.["id"]
                        let mtable = db.Tables.["Mentions"]
                        for user in mentions do
                            let exp = "username = '"+user+"'"
                            let utable = db.Tables.["Users"]
                            let dataRows = utable.Select(exp)
                            if dataRows.Length <> 0 then
                                let row = mtable.NewRow()
                                let u = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
                                tweetMentions <- u :: tweetMentions
                                row.["tweetid"] <- tweetrow.["id"]
                                row.["userid"] <- u.ID
                                mtable.Rows.Add row
                        let tweetObject = Tweet(int(string(tweetrow.["id"])),string(tweet),tweetHashtags,tweetMentions)
                        let mutable i = 0
                        for sub in subs do
                            if loggedInUsers.ContainsKey(sub.ID) then
                                loggedInUsers.[sub.ID] <! CTweetUpdate(tweetObject,c,false)
                                i <- i + 1
                        // printfn "%A: Tweet sent to %A logged in subscribers" c.Username i
                        totalTweets <- totalTweets + 1
                        expectedToSend <- expectedToSend + subs.Count
                        actualToSend <- actualToSend + i
                        mailbox.Sender() <! TweetSent
                    // else
                    //     mailbox.Sender() <! NotAuthorised
                | RetweetRequest (c, tweet,subs) ->
                    if loggedInUsers.ContainsKey(c.ID) then
                        let table = db.Tables.["Retweets"]
                        let row = table.NewRow()
                        row.["tweetid"] <- tweet.ID
                        row.["userid"] <- c.ID
                        table.Rows.Add row
                        let mutable i = 0
                        for sub in subs do
                            if loggedInUsers.ContainsKey(sub.ID) then
                                loggedInUsers.[sub.ID] <! CTweetUpdate(tweet,c,true)
                                i <- i + 1
                        printfn "%A: Retweet sent to %A logged in subscribers" c.Username i
                        totalTweets <- totalTweets + 1
                        expectedToSend <- expectedToSend + subs.Count
                        actualToSend <- actualToSend + i
                        mailbox.Sender() <! TweetSent
                    // else
                    //     mailbox.Sender() <! NotAuthorised
                | SimulatorStats ->
                    printfn "************************************************************************************"
                    printfn "Total users logged to server: %A" totalLogged 
                    printfn "Total number of tweets sent by users: %A" totalTweets
                    printfn "Expected number of tweets to be received by users: %A" expectedToSend
                    printfn "Number of tweets received to logged users: %A" actualToSend
                // | _ -> printfn "request unknown"

                return! loop ()
            }
        loop ()