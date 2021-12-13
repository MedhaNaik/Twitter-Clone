# Distributed Operating System Principles Project 4(2): TwitterAPI

## Group members :
- 1.Rohit Karumuri - 9097-1158
- 2.Spandana Edupuganti - 1733-6715

##  About the Project:
    This is a Twitter Api Websocket built using the Suave. We have a server which is the main server which communicates with database. It consists of all data of tweets, retweets, hashtags and mentions. The server acts as an engine helpful to distribute tweets. The Web socket interface is used for the registering the account, creating tweets along with hashtags and mentions. Each web socket interface has an akka actor node which is the medium to communicate with the server. These nodes will handle all the communication from the socket. The client testing is done via a browser interface.

## Instructions to run code :
   1. Unzip the file
   2. Build the project ~dotnet build
   3. Run the project ~dotnet run   

## Link for video demonstration of Twitter:

Demo.mkv


## Result
    1. In the application, we output the tweets of the user, success messages and error messages. 
    2. The users will be able to subscribe to the other users. 
    3. The subscribers will receive tweets from the tweeted users.

