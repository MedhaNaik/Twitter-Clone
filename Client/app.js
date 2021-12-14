
var wsUri = "ws://localhost:8080/websocket";
var websocket;
var user = "";

var logged = false;
var tweets = [];
var results = [];

  function init()
  {
    testWebSocket();
    checkLogin();
  }

  function testWebSocket()
  {
    websocket = new WebSocket(wsUri);
    websocket.onopen = function(evt) { onOpen(evt) };
    websocket.onclose = function(evt) { onClose(evt) };
    websocket.onmessage = function(evt) { onMessage(evt) };
    websocket.onerror = function(evt) { onError(evt) };
  }

function handleMessage(res) {
  console.log(res);
  if (res.response == "UserRegistered" || res.response == "UserExists") {
    user = res.data;
    tweets = [];
    var pass = document.getElementById("password").value;
    var jmsg = {
      request: "Login",
      username: user,
      password: pass,
      tweet: "",
      mentions: [],
      hashtags: []
    };
    doSendJson(jmsg);
  }
  else if (res.response == "UserLogged") {
    user = res.data;
    logged = true;
  }
  else if (res.response == "LoggedOut") {
    user = "";
    logged = false;
    document.getElementById("username").value = "";
    document.getElementById("password").value = "";
    alert("Logged Out!");
    tweets = [];
  } else if (res.response == "Tweet") {
    tweets.push(res.data);
  } else if (res.response == "TweetSent") {
    document.getElementById("username").value = "";
    document.getElementById("tweet").value = "";
    document.getElementById("mentions").value = "";
    document.getElementById("hashtags").value = "";
    document.getElementById("subscribeText").value = "";
  }
  else if (res.response == "ERROR") {
    alert(res.data);


    
  }
  else if (res.response == "SearchResult")
  {
    document.getElementById("searchResult").value = "";
    $("#searchResult").empty();
    
    res.data.forEach(element => {
      $("#searchResult").append("<p>" + element + "</p>");
    });
    
  }
}

  function onOpen(evt)
  {
    console.log("LoggedIn!");
  }

  function onClose(evt)
  {
    alert("LoggedOut!");
      user = "";
    logged = false;
    tweets = [];
  }

  function onMessage(evt)
  {
    var res = JSON.parse(evt.data);
    handleMessage(res);
  }

  function onError(evt)
  {
  }

  function doSend(message)
  {
    websocket.send(message);
  }
  function doSendJson(message)
  {
    var msg = JSON.stringify(message);
    websocket.send(msg);
}
function checkLogin() {
  if (logged && user != "") {
    // logged in
    $("#loginPage").addClass("hide");
    $("#profilePage").removeClass("hide");
    doSendJson({
      request: "Refresh",
      username: user
    });
    $("#tweetArea").empty();
    tweets.forEach(element => {
      $("#tweetArea").append("<p>" + element + "</p>");
    });


  } else {
    $("#loginPage").removeClass("hide");
    $("#profilePage").addClass("hide");
    $("#userHead").remove("#userSpan");
    $("#tweetArea").empty();
    }
}
  $(document).ready(function () {
    init();
    setInterval(checkLogin, 2000);

    $("#login").click(function () {
      var ustext = document.getElementById("username").value;
      var pass = document.getElementById("password").value;
      if (ustext != "" && !logged) {
        var jmsg = {
          request: "Login",
          username: ustext,
          password: pass,
          tweet: "",
          mentions: [],
          hashtags: []
        };
        doSendJson(jmsg);
      }
    });

    $("#register").click(function () {
      console.log("reg clicked")
      var ustext = document.getElementById("username").value;
      var pass = document.getElementById("password").value;
      if (ustext != "" && !logged) {
        var jmsg = {
          request: "Register",
          username: ustext,
          password: pass,
          tweet: null,
          mentions: null,
          hashtags: null
        };
        doSendJson(jmsg);
      }
    });

    $("#searchButton").click(function () {
      console.log("reg clicked")
      var search = document.getElementById("search").value;
      if (ustext != "" && !logged) {
        var jmsg = {
          request: "Search",
          username: user,
          search: search,
          tweet: null,
          mentions: null,
          hashtags: null
        };
        doSendJson(jmsg);
      }
    });



    $("#logoutButton").click(function () {
      if (logged && user != "") {
        var jmsg = {
          request: "Logout",
          username: user,
          tweet: null,
          mentions: null,
          hashtags: null
        };
        doSendJson(jmsg);
      }
    });
    $("#tweetButton").click(function () {
      var ustext = document.getElementById("tweet").value;
      var mtext = document.getElementById("mentions").value;
      var ms = mtext.split(" ");
      ms.forEach(m => {
        ustext += " @" + m; 
      });
      var htext = document.getElementById("hashtags").value;
      var hs = htext.split(" ");
      hs.forEach(m => {
        ustext += " #" + m; 
      });
      tweets.push("You tweeted " + ustext);
      if (logged && user != "") {
        var jmsg = {
          request: "Tweet",
          username: user,
          tweet: ustext,
          mentions: ms,
          hashtags: hs
        };
        doSendJson(jmsg);
      }
    });
    $("#subscribeButton").click(function () {
      var ustext = document.getElementById("subscribeText").value;
      if (ustext != "" && logged) {
        if (ustext.charAt(0) == "#") {
          var jmsg = {
            request: "Subscribe",
            username: user,
            tweet: "",
            subtype: "hashtag",
            subTo: ustext.substring(1),
            mentions: [],
            hashtags: []
          };
          doSendJson(jmsg);
        } else {
          var jmsg = {
            request: "Subscribe",
            username: user,
            tweet: "",
            subtype: "user",
            subTo: ustext,
            mentions: [],
            hashtags: []
          };
          doSendJson(jmsg);
        }
       
      }
    });

  });