// should keep the conditional comment
var ie/*@cc_on=1@*/;

// should keep the conditional comment and the conditional variable and don't
// add a cc_on to the comment (we already encountered one and therefore don't need it)
var ver//@ =@_jscript_version

// keep the conditional comment, don't add the cc_on, and don't throw an error
// because there is no space between the @ and the =
var ack//@=2

// combination of variables and preprocess values
// (and don't add a cc_on)
var foo//@cc_on = (ver + !@bar) * 12

// does NOT fit the special-case pattern, so don't keep the conditional comment
var isMSIE = /*@cc_on!@*/0;

// this is just so we know that last one doesn't kill the entire processing and can recover okay
alert(ver);