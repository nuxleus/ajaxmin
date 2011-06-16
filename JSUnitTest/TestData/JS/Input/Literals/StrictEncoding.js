function myStrict(a,b)
{
    // strict mode
    "use strict"
    a += b;
    b = +a * 200;

    // strict mode cannot encode the \x02 as octal \2
    return "one\x02two" + a + b;
}

function notStrict(a,b)
{
    a += b;
    b = +a * 200;

    // unrestricted mode (not strict) will encode the \x02 as octal \2
    return "one\x02two" + a + b;
}