function foo()
{
    // combine
    var bar = window.location;
    return bar;
}

function bar(x, y)
{
    // combine
    var foo = (3 + x) * y;
    return foo;
}

function arf()
{
    // DON'T combine
    var foo = "bar";
    return;
}

function gag(a, b)
{
    // DON'T combine
    var foo = a * b;
    return foo + a;
}
