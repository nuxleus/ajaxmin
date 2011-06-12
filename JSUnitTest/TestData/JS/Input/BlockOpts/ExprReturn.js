function test1(a, b, c)
{
    // transform to return c=a(b)
    c = a(b);
    return c;
}

function test2(a, b, c)
{
    a.foo.bar = b + c;
    return a.foo.bar;
}

function test3(a, b, c)
{
    a[b + c] = a + b + c;
    return a[b+c];
}