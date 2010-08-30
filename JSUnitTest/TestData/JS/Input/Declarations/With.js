
// because the with statement marks the function as UNKNOWN, 
// even if we hypercrunch this code, it should still look the same.
var someGlobal = 12;
var unreferencedGlobal = 42;

function Func(p1)
{
    var x, y;
    var unrefLocal = 16;
    
    with (Math)
    {
        x = cos(3 * PI) + sin (LN10);
        y = tan(14 * E);
        function wham(){ return y; }
    }
    
    function foobar()
    {
      with( p1 )
      {
        var toodles = goodbye;
        foo = x;
        bar = someGlobal * 2;
        
        with( toodles )
        {
          var tart = treacle;
        }
      }
    }
    
}