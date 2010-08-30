

var \while = 10; // escaping the identifier seems to make it an ident token and not a statement token
var \for = function() {};

for(var i = 0; i < 10; ++i)
{
  \for(\while);
}

while( \while < 200 )
{
  \while += 90;
}


