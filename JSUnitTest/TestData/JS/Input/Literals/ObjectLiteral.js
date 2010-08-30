var o = {
  get ack() { return 42; },
  set ack(v) { alert(v); },
  123 : 4.56e+03,
  789.2 : true,
  "help" : "me",
  "while" : 45.67,
  foo : function() {return "bar";},
  goto : "what?",
  "你好" : "hello"
  };
 
alert(o.goto);
