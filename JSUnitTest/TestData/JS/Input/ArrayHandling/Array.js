function Func(p1)
{
    var arrayObj1 = new Array();            // crunch to []
    var arrayObj2 = new Array(1);           // single numeric argument is size, not initializer. Don't crunch to literal.
    var arrayObj3 = new Array(1, 2, 3, 4);  // crunch to [1,2,3,4]
    var arrayObj4 = new Array;              // crunch to []
    var arrayObj5 = [["Names", "Beansprout", "Pumpkin", "Max"], ["Ages", 6,, 4]]; // missing array item
    var arrayObj6 = [6, 5, 4];              // crunch to [6,5,4]
    var arrayObj7 = new Array("foo");       // single non-numeric is not size; crunch to ["foo"]
    
    // single argument, but we don't know the type. don't crunch to literal
    var arrayObj8 = new Array(arrayObj7.length);
    var arrayObj9 = new Array(arrayObj1);
    var arrayObj10= new Array(foo());
}
