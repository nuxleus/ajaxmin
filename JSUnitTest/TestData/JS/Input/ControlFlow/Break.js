function Func(p1)
{
    var i = 0;
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
   return(i);
}