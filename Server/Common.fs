namespace TwitterAPI
// module Common
open System.Collections.Generic
open Akka.Configuration
open System


module Common =
        
    type queryType =
        | MENTIONS 
        | SUBSCRIPTION 
        | HASHTAG

    type feedType =
        | MENTIONS_FEED
        | SUBSCRIPTION_FEED
        | HASHTAG_FEED

    type subscriptionType =
        | HASHTAG_SUB
        | USER_SUB

    // Tweet format
    type tweet() = 
        inherit obj()
        [<DefaultValue>] val mutable guID: string
        [<DefaultValue>] val mutable senderID: string
        [<DefaultValue>] val mutable body: string
        [<DefaultValue>] val mutable mentions: Set<string>
        [<DefaultValue>] val mutable hashtags: Set<string>

    type query() =
        inherit obj()
        [<DefaultValue>] val mutable queryType: queryType
        [<DefaultValue>] val mutable queryString: string
        [<DefaultValue>] val mutable result: List<tweet>

    type subscription() =
        inherit obj()
        [<DefaultValue>] val mutable subType: subscriptionType
        [<DefaultValue>] val mutable subTo: string

    // messages to server entrypoint
    type ServerMessage = 
        | REGISTER of string * string // sender userID, password
        | LOGIN of string * string // sender userID, password
        | LOGOUT of string // sender userID
        | TWEET of tweet // tweet
        | RETWEET of string*string // sender userID, tweetID to retweet
        | SUBSCRIBE of string*subscription // sender userID, subscription
        | QUERY of string*query // sender userID, query


        



//----------------------------------------------------------

// INTERNAL SERVER ENGINE SIDE MESSAGES 


    // messages to response actor
    type ResponseMessage = 
        | SEND_REGISTERED of Akka.Actor.IActorRef
        | SEND_LOGGEDIN of Akka.Actor.IActorRef 
        | SEND_LOGGEDOUT of Akka.Actor.IActorRef 
        | SEND_ERROR of Akka.Actor.IActorRef*string
        | SEND_FEED of string*tweet*feedType
        | SEND_RESULT of string*query


    // messages to tweet actor
    type FeedMessage = 
        | FEED of string*tweet 
        | NEWQUERY of string*query
        | NEWSUB of string*subscription

    type ClientReq =    
        | LOGIN_REQUEST of username: string * password: string
        | LOGOUT_REQUEST of username:string
        | REGISTER_REQUEST of username:string * password: string
        | USERSUB_REQUEST of suser: string * subTo:string
        | HASHTAG_SUB_REQUEST of suser: string * sHashtag: string
        | TWEET_REQUEST of cuser: string * tweet: string * hashtags: Set<string> * mentions: Set<string>
        | REFRESH_REQUEST of username:string
        | NEW_FEED of feedType*tweet
    

        // messages to end user
    type ClientMessage = 
        | ACT
        | TWEETACTION
        | DOLOGIN
        | DOLOGOUT
        | INIT of string
        | REGISTERED 
        | LOGGEDIN 
        | LOGGEDOUT 
        | ERROR of string // error message
        | SUBSCRIBED of subscription
        | QUERY_RESULT of query
    
    // json types for serialization
    type JRequest = {
        request:string;
        username: string;
        password: string;
        subtype: string;
        subTo: string;
        tweet:string;
        mentions:Set<string>;
        hashtags: Set<string>
    } 

    type JResponse = {
        response:string;
        data: string
    }

    

    let configuration = 
        ConfigurationFactory.ParseString(
            @"akka {
                actor {
                    provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                    serializers {
                        hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
                    }
                    serialization-bindings {
                    ""System.obj"" = hyperion
                    }
                    debug : {
                        receive : on
                        autoreceive : on
                        lifecycle : on
                        event-stream : on
                        unhandled : on
                    }
                }
                remote {
                    helios.tcp {
                        transport-protocol = tcp
                        port = 9002
                        hostname = localhost
                    }
                }
            }")
