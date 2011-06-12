function multi(a, b)
{
    var x = a + b,
        y = a - b,
        ret = x * y;
    return ret;
}

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

function test1(a, b)
{
    // shouldn't combine because the initializer expression
    // also references the variable -- if we get rid of the 
    // var, we lose the field and it will error.
    var c = a + (c = b) + b;
    return c;
}

function test2(a,b)
{
    // same deal -- can't combine the var and the return because the var
    // is referenced somewhere other than the return
    c = 10;
    var c = a + b;
    return c;
}