function foo(){for(var a=0;a<10;++a)switch(a){case 1:a=0;break;case 5:a=1;continue;case 8:a=2;break;case 10:a=3;throw"error";case 12:a=4;return-1}}