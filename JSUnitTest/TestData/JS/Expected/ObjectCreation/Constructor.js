function Func(p1){p1=new String("Hi");p1.constructor==String&&alert("Hi")}function MyFunc(){this.Foo="1";this.Bar="2";this.Ack="3";this.Gag="4";function arf(){this.Ralph="first";this.Cramden="last"}this.Barf=new arf}var y=new MyFunc;y.constructor==MyFunc&&alert("My")