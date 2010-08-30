function b(n){return Number;}
var a, s;

// parentheses must NOT go away
s = new (a || b)(12),

// these parentheses can go away
s = (new Number(12)).toString();

// do not add parentheses (same as above statement)
s = new Number(12).toString();

// don't add any parens, but remove the empty arguments
s = new Number.toString(); // same as (new (Number.toString)())
    
// the empty arguments go away, which means the new operator needs to be wrapped in parens
s = new Number().toString(); // same as (new Number).toString()
    
// these parentheses must NOT go away because there are no arguments
s = (new Number).toString(); // same as new Number().toString()
    
// the new operator has no arguments, but we need to add parens because the empty args will go away
// and we don't want the call arguments to be usurped
s = (new Number)(12); // same as new Number()(12)
    
// the new operator has no arguments, but we need to add parens because the empty args will go away
// and we don't want the call arguments to be usurped
s = new Number()(12); // same as above
    
// the new operator has no arguments, but we need to add parens because the empty args will go away
// and we don't want the member-bracket arguments to be usurped
s = (new Number)[12]; // same as new Number()(12)
    
// the new operator has no arguments, but we need to add parens because the empty args will go away
// and we don't want the member-bracket arguments to be usurped
s = new Number()[12]; // same as above

// these parens must NOT go away
s = new (b(2))(1234); // 

// must keep parentheses around b(1), remove empty argument parens,
// then add parens around the new operator
s = new (b(1))().toString(); // (new (b(1))).toString()
