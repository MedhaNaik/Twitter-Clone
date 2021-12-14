
var wsUri = "ws://localhost:8080/websocket";
var websocket;
var user = "";
var logged = false;
var tweets = [];

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
    logged = true;
    alert("Logged in as " + user);
    tweets = [];
  }
  else if (res.response == "UserLogged") {
    user = res.data;
    logged = true;
    alert("Logged in as " + user);
  }
  else if (res.response == "LoggedOut") {
    user = "";
    logged = false;
    alert("Logged Out!");
    tweets = [];
  } else if (res.response == "Tweet") {
    tweets.push(res.data);
  } else if (res.response == "TweetSent") {
    document.getElementById("usernameInput").value = "";
    document.getElementById("tweetTextarea").value = "";
    document.getElementById("mentionsInput").value = "";
    document.getElementById("hashtagsInput").value = "";
    document.getElementById("subInput").value = "";
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

    $("#loginBtn").click(function () {
      var ustext = document.getElementById("usernameInput").value;
      if (ustext != "" && !logged) {
        var jmsg = {
          request: "Login",
          username: ustext,
          tweet: "",
          mentions: [],
          hashtags: []
        };
        doSendJson(jmsg);
      }
    });

    $("#registerBtn").click(function () {
      var ustext = document.getElementById("usernameInput").value;
      if (ustext != "" && !logged) {
        var jmsg = {
          request: "Register",
          username: ustext,
          tweet: null,
          mentions: null,
          hashtags: null
        };
        doSendJson(jmsg);
      }
    });

    $("#logoutBtn").click(function () {
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
    $("#tweetBtn").click(function () {
      var ustext = document.getElementById("tweetTextarea").value;
      var mtext = document.getElementById("mentionsInput").value;
      var ms = mtext.split(" ");
      ms.forEach(m => {
        ustext += " @" + m; 
      });
      var htext = document.getElementById("hashtagsInput").value;
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
    $("#subBtn").click(function () {
      var ustext = document.getElementById("subInput").value;
      if (ustext != "" && logged) {
        var jmsg = {
          request: "Subscribe",
          username: ustext,
          tweet: "",
          mentions: [],
          hashtags: []
        };
        doSendJson(jmsg);
      }
    });

  });