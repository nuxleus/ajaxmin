@if (@_jscript_version > 3)
alert("gt 3");
//@elif (@_jscript_version == 8)
alert("eq 8");
/*@elif (@_jscript_version > 8)@*/
alert("gt 8");
@else
alert("le 3");
@end

