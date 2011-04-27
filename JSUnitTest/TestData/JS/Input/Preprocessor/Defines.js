

///#UNDEF notdefined
///#DEFINE foobar

///#IFDEF foobar
var a = "foobar";
///#ELSE
var a = "not foobar";
///#ENDIF

///#UNDEF      foobar

///#IFDEF foobar
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

