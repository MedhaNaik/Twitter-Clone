namespace TwitterAPI

open System
open System.Collections.Generic
open System.Data
open System.IO
open MathNet.Numerics.Distributions

module Database = 
    let db:DataSet = new DataSet()
    let userTable:DataTable = new DataTable("Users")
    let subscriberTable:DataTable = new DataTable("Subscribers")
    let tweetTable:DataTable = new DataTable("Tweets")
    let retweetTable:DataTable = new DataTable("Retweets")
    let hashtagTable:DataTable = new DataTable("Hashtags")
    let hashtagTweetTable:DataTable = new DataTable("Hashtag-Tweet")
    let mentionTable:DataTable = new DataTable("Mentions")
    
    let createUserTable () = 
        // usertable
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        userTable.Columns.Add(idColumn)
    
        let usernameColumn:DataColumn = new DataColumn()
        usernameColumn.ColumnName <- "username"
        usernameColumn.DataType <- System.Type.GetType("System.String")
        usernameColumn.Unique <- true
        userTable.Columns.Add(usernameColumn)
    
        userTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(userTable)
    
    let createSubscriberTable () = 
        // subscribertable
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        subscriberTable.Columns.Add(idColumn)
        let uidColumn:DataColumn = new DataColumn()
        uidColumn.ColumnName <- "userid"
        uidColumn.DataType <- System.Type.GetType("System.Int32")
        subscriberTable.Columns.Add(uidColumn)
        let sidColumn:DataColumn = new DataColumn()
        sidColumn.ColumnName <- "subscriberid"
        sidColumn.DataType <- System.Type.GetType("System.Int32")
        subscriberTable.Columns.Add(sidColumn)
        subscriberTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(subscriberTable)
    
    let createTweetTable () =
        // tweettable
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        tweetTable.Columns.Add(idColumn)
    
        let tweetColumn:DataColumn = new DataColumn()
        tweetColumn.ColumnName <- "tweet"
        tweetColumn.DataType <- System.Type.GetType("System.String")
        tweetTable.Columns.Add(tweetColumn)
    
        let uidColumn:DataColumn = new DataColumn()
        uidColumn.ColumnName <- "userid"
        uidColumn.DataType <- System.Type.GetType("System.Int32")
        tweetTable.Columns.Add(uidColumn)
    
        tweetTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(tweetTable)
    
    let createRetweetTable () =
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        retweetTable.Columns.Add(idColumn)
    
        let tidColumn:DataColumn = new DataColumn()
        tidColumn.ColumnName <- "tweetid"
        tidColumn.DataType <- System.Type.GetType("System.Int32")
        retweetTable.Columns.Add(tidColumn)
    
        let uidColumn:DataColumn = new DataColumn()
        uidColumn.ColumnName <- "userid"
        uidColumn.DataType <- System.Type.GetType("System.Int32")
        retweetTable.Columns.Add(uidColumn)
        retweetTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(retweetTable)
    
    let createHashtagTable () =
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        hashtagTable.Columns.Add(idColumn)
    
        let hashtagColumn:DataColumn = new DataColumn()
        hashtagColumn.ColumnName <- "hashtag"
        hashtagColumn.DataType <- System.Type.GetType("System.String")
        hashtagColumn.Unique <- true
        hashtagTable.Columns.Add(hashtagColumn)
        hashtagTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(hashtagTable)
    
    let createHashtagTweetTable () =
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        hashtagTweetTable.Columns.Add(idColumn)
    
        let hidColumn:DataColumn = new DataColumn()
        hidColumn.ColumnName <- "hashtagid"
        hidColumn.DataType <- System.Type.GetType("System.Int32")
        hashtagTweetTable.Columns.Add(hidColumn)
    
        let tidColumn:DataColumn = new DataColumn()
        tidColumn.ColumnName <- "tweetid"
        tidColumn.DataType <- System.Type.GetType("System.Int32")
        hashtagTweetTable.Columns.Add(tidColumn)
        hashtagTweetTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(hashtagTweetTable)
    
    let createMentionTable() = 
        let idColumn:DataColumn = new DataColumn()
        idColumn.ColumnName <- "id"
        idColumn.DataType <- System.Type.GetType("System.Int32")
        idColumn.AutoIncrement <- true
        idColumn.AutoIncrementSeed <- 1L
        idColumn.AutoIncrementStep <- 1L
        mentionTable.Columns.Add(idColumn)
        
        let tidColumn:DataColumn = new DataColumn()
        tidColumn.ColumnName <- "tweetid"
        tidColumn.DataType <- System.Type.GetType("System.Int32")
        mentionTable.Columns.Add(tidColumn)
    
        let uidColumn:DataColumn = new DataColumn()
        uidColumn.ColumnName <- "userid"
        uidColumn.DataType <- System.Type.GetType("System.Int32")
        mentionTable.Columns.Add(uidColumn)
    
        mentionTable.PrimaryKey <- [|idColumn|]
        db.Tables.Add(mentionTable)
    
    let populateData (n) =
        for i in 1..n do
            let row:DataRow = userTable.NewRow()
            row.[1] <- "user" + (string) i
            userTable.Rows.Add(row)
        (*
        let subdata = File.ReadAllLines "subscribers.txt"
        for line in subdata do
            let row:DataRow = subscriberTable.NewRow()
            let ll = line.Split(",")
            row.[1] <- (int)ll.[1]
            row.[2] <- (int)ll.[2]
            subscriberTable.Rows.Add(row)
        *)
        let lim = if n < 100 then n else 100
        let samples = List<int>()
        let mutable k = 0
        while k < n do
            samples.Add(Zipf.Sample(1.0,lim))
            k <- k + 1
        samples.Sort()
        samples.Reverse()
        let rand = Random()
        for i in [1..n] do
            for j in [0..samples.[i-1]-1] do 
                let mutable ri = rand.Next(1,n)
                while ri=i do
                    ri<- rand.Next(1,n)
                let row:DataRow = subscriberTable.NewRow()
                row.[1] <- i
                row.[2] <- ri
                subscriberTable.Rows.Add(row)
    
    let makeDatabase (n) =
        createUserTable()
        createSubscriberTable()
        createTweetTable()
        createRetweetTable()
        createHashtagTable()
        createHashtagTweetTable()
        createMentionTable()
        populateData(n)