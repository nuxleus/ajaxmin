function Func(p1)
{
    var i = 0, j = 0;
    while (i < 100)
    {
        if (i == p1)
        {
            break;
        }
        i++;
    }

    for(i=0; i < 100; i++)
    {
        if (i == p1)
        {
            break;
        }
    }
   
    for (i = 0; i < 10; i++, j++)
    {
       p1 = i + j;
    }
    
    for(;;)
    {
        break;
    }
    while(1)
    {
        break;
    }
    
    // empty body on the while
    while(--i > 0);
    
    // empty body on the for
    for(var t = 10, y=5; t > 0; t--, y+=y)
    {
    }

   return(i);
}