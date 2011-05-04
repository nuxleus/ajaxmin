

///#UNDEF notdefined
///#DEFINE FooBar

///#IFDEF foobar
var a = "foobar";
///#ELSE
var a = "not foobar";
///#ENDIF

///#UNDEF      foobar

///#IFDEF FOOBAR
var b = "foobar";
///#ELSE
var b = "not foobar";
///#ENDIF

///#IFDEF ackbar
var c = "ackbar";
///#ELSE
var c = "not ackbar";
///#ENDIF

///#IFDEF meow
function meow()
{
    alert("MEOW!");
}
///#ENDIF

