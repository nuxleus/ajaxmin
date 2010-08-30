// negative zero is a valid numeric value different than positive zero
// positive zero is the same as zero with no sign
var dz1 = 0, dz2 = -0, dz3 = +0;

// decimal
var d1=10, d2=1.23e10, d3=1.2e+5, d4=1.3e-5, d5=1.45, d6=-42, d7=-1.414e-0, d8=.666, d9=+5, d10=+.6, d11=+1.66e10;

// hexadecimal
var h1=0x19, h2=0xa, h3=0xffff, h4=0x0001, h5=0xfedcba9876, h6=-0xff;

// octal
var o1 = 0377, o2 = 012, o3 = 00, o4 = 089;

// literal representing the maximum numeric value. should suggest developer replace with Number.MAX_VALUE
// while leaving the value literal crunched but still numeric
var max = 1.7976931348623157E+308;
// literal representing the minimum numeric value. should suggest developer replace with Number.MIN_VALUE
// while leaving the value literal crunched but still numeric
var min = -1.7976931348623157E+308;

// these values are commonly used to represent min and max values, but they are NOT correct.
// browsers evaluate them as Infinity and -Infinity. We need to throw an error telling the developer
// that these values cause an overflow, but we should echo them unchanged into the output --
// not crunched at all or replace with Numeric.POSITIVE_INFINITY or anything in case that would cause
// unforeseen side-effects
var pos = 1.79769313486232E308;
var neg = -1.79769313486232E+308;

// and this one is just a blatant greater-than-max infinity value that should throw an error
// but be replaced as-is without crunching
var tooBig = 1E999;

// boundary conditions for floating-point
var b = 123456789012345678901 + 12345678901234567891;

// overflow for an object-literal field name works in browsers. for instance,
// if obj[1e999] = 2 then obj[1e999] === obj[1e309] evaluates to true because
// the index is just Number.POSITIVE_INFINITY.
var obj = {1e969: "pos inf"};

