
function foo()
{
    var a = 1,
        b = 2,
        c = "three",
        d,
        f;
    try
    {
        d = a * b;
        while(d > 0)
        {
            switch(c)
            {
                case"three":
                    c = 10;
                    break;
                case 0:
                    c = -1;
                    break;
                default:
                    c = c * 2
            }
            c ? (d = d * c, a *= d) : c = 1
        }
    }
    catch(e)
    {
        for(f = 0; f < b; ++f)
            a = a * d
    }
    finally
    {
        b = a ? a : -1
    }
}
