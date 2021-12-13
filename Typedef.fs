namespace TwitterAPI
// module Typedef
open System.Collections.Generic
open Akka.Actor
open Akka.FSharp

module Typedef =

    type User(id: int, un: string) =
        member this.ID: int = id
        member this.Username: string = un
        new() = User(-1, "")
    
    type Hashtag(id: int, hashtag: string) =
        member this.ID: int = id
        member this.Value: string = hashtag
    
    type Tweet(id: int, t: string,h:Hashtag list, m:User list) =
        member this.ID: int = id
        member this.Value: string = t
        member this.Hashtags: Hashtag list = h
        member this.Mentions: User list = m
        new(id,t) = Tweet(id,t,[],[])
    
    type TweetMessage =
        struct
            val tweet:Tweet
            val user:User
            val retweet:bool
            val mention:bool
            new(t:Tweet,u:User,r:bool,m:bool) = {tweet=t;user=u;retweet=r;mention=m}
        end
    
    type ServerRequest =
        | LoginUser of username: string * mailbox: IActorRef
        | LogoutUser of user:User
        | RegisterUser of username:string * mailbox: IActorRef
        | SubscribeUser of cuser: User * suser: User
        | SubscribeUserA of cuser:User * suser: string
        | TweetRequest of cuser: User * tweet: string * hashtags: string list * mentions: string list * subscribers:    List<User>
        | RetweetRequest of cuser: User * tweet: Tweet * subscribers:List<User>
        | SimulatorStats
    
    type ServerResponse =
        | StartSimulation
        | EndSimulation
        | UserLogged of user: User * subscribers: List<User> * tweetMessages: List<TweetMessage>
        | UserNotFound
        | UserLoggedOut
        | NotAuthorised
        | UserRegistered of user:User
        | UserExists of user:User
        | UserSubscribed
        | SubscriberNotFound
        | AlreadySubscribed
        | TweetSent
        | TweetUpdate of tweet:Tweet * user:User * retweet:bool
        | TweetChecker
        | TweetTrigger
        | StartRegisterSim
        
    type ClientReq =    
        | CLoginUser of username: string
        | CLogoutUser of username:string
        | CRegisterUser of username:string
        | CSubscribeUser of suser: string
        | CTweetRequest of cuser: string * tweet: string * hashtags: string list * mentions: string list
        | CRetweetRequest of cuser: User * tweet: Tweet * subscribers:List<User>
        | CRefresh of username:string
        | CTweetUpdate of tweet:Tweet * user:User * retweet:bool
        | CSubscriberNotFound
        | CAlreadySubscribed
        | CUserSubscribed
        | CAddSubscriber of suser:User
    
    // json types for serialization
    type JRequest = {
        request:string;
        username: string;
        tweet:string;
        mentions:string list;
        hashtags: string list
    } 
    type JResponse = {
        response:string;
        data: string
    }