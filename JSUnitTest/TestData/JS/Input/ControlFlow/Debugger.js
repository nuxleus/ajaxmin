var i = new Image();

while(0)
{
  $Debug.Write("nope");
}
for(var p in i)
{
  $Debug.Write(p);
}
for(var n=0; n < i.length; ++n)
{
  $Debug.Write(n);
}
do
{
  $Debug.Write(i);
}while(0);

try
{
  i.src = "foo"; 
}
catch(e)
{
  $Debug.Write(e);
}
finally
{
  n = i;
}

// ends in semicolon, no braces
if ( !i )
    debugger;

// no semicolon, other statement on next line
if ( !i )
{
    debugger
    i = null;
}
// no semicolon, next token a right-brace
if ( !i ) { debugger }

// this is a call to the Atlas framework debug namespace.
// it should get stripped out with the -d option
Web.Debug.Write("foo");
Web.Debug.ASSERT("foo")();

// same for this one
$Debug.Write("arf");

// also trim any calls into the Debug namespace
Debug.assertParam("foo");
Debug.assertType("foo");
Debug.assert("foo");
Debug.failIf("foo");
Debug.fail("foo");
Debug.writeLine("foo");

// also trim WAssert calls
WAssert("blah");

// but this one is used as a constructor -- it should get
// replaced with an empty object constructor
var foo = new $Debug.DebugWindow(1);

///#DEBUG
// we will skip these statements if we are stripping debug statements
alert("DEBUG!");
///#ENDDEBUG

//@cc_on
//@if(@DEBUG)
foo(bar);
//@end




