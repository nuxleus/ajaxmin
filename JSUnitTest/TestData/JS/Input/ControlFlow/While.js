
// while(true) gets changed to for(;;)
// (and then the previous var statement gets sucked into the for)
var b = 10;
while(true)
{
    if (!--b) break;
}
