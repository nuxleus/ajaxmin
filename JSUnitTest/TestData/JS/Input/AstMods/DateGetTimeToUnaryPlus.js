﻿// convert the pattern (new Date()).getTime() to +new Date
// BUT only if the Date lookup turns out to be the built-in reference, not a local.
// AND neither getTime nor the constructor can have any arguments.

var ticks = (new Date()).getTime(); // changed to var ticks=+new Date;

function foo()
{
    function Date(){};
    ticks = (new Date()).getTime(); // doen't get changed -- not the global "Date" constructor
}

function bar(dt)
{
    var ack = new Date(dt).getTime(); // converted to +new Date("1/20/2009")
    var gag = new Date().getTime("foo"); // getTime has arguments! No conversion

    // make sure the pluses keep a space between them so they don't get read as an increment
    // (just to make sure)
    var loo = gag + (new Date).getTime(); // c=b+ +new Date
}

// make sure the expression stays the same
var nextYear = new Date(new Date().getTime() + 365 * 24 * 3600000);
