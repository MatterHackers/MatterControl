/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.MatterControl.PluginSystem;
#if !__ANDROID__
using NUnit.Framework;
#endif
using System;
using System.Collections.Generic;
using System.Text;

// finish a, b, t

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public static class BrailleGrade2Mapping
	{
		public static string mappingTable =
			// + means needs one or more lettes before or after
			// * means needs one or more words before or after
			// all caps means needs to be exact match
@"
// a's
about ab
above abv
according ac
across acr
after af
afternoon afn
afterward afw
again ag
against ag.
+ally ,y
almost alm
already alr
also al
although al?
altogether alt
always alw
+ance .e
and &
ar >
AS z
+ation ,n

// b's
+bb+ 2
BE 2
be+ 2
because 2c
before 2f
behind 2h
below 2l
beneath 2n
beside 2s
between 2t
beyond 2y
+ble #
blind bl
braille brl
BUT b
BY* 0

// c's
CAN c
cannot _c
+cc+ -
ch *
character ""*
CHILD *
children *n
com+ -
con+ 3
conceive 3cv
conceiving 3cvg
could cd

// d's
day
+dd+
deceive
deceiving
declare
declaring
dis+
DO

// e's
+ea+
ed
either ei
en 5
+ence
ENOUGH
er }
ever
EVERY e

// f's
father
+ff+
first
for
FROM f
+ful

// g's
+gg+
gh
GO g
good gd
great grt

// h's
had
HAVE h
here ""h
herself
him hm
himself hmf
HIS

// i's
in
ing
INTO
IT x
ITS xs
itself xf
+ity

// j's
JUST j

// k's
know 
KNOWLEDGE k

// l's
+less
letter lr
LIKE l
little ll
lord

// m's
many _m
+ment ;t
MORE m
mother ""m
much m*
must m/
myself myf

// n's
name ""n
necessary nec
neither nei
+ness ;s
NOT n

// o's
o'clock o'c
of (
one ""o
oneself ""of
+ong ;g
ou |
ought ""|
+ound .d
+ount .t
ourselves |rvs
OUT |
ow {

// p's
paid pd
part ""p
PEOPLE p
percieve p}cv
perceiving p}cvg
perhaps p}h

// q's
question ""q
quick qk
QUITE q

// r's
RATHER r
receive rcv
receiving rcvg
rejoice rjc
right ""r

// s's
said sd
sh %
SHALL %
should %d
+sion .n
SO s
some ""s
spirit _s
st /
STILL /
such s*

// t's
th ?
THAT t
the !
their _!
themselves !mvs
there ""!
these ~!
THIS ?
those ~?
through _?
thyself ?yf
time ""t
tion ;n
to* 6 // must hava word after and then does not leave the space
today td
together tgr
tomorrow tm
tonight tn

// u's
under ""u
upon ~u
US u

// v's 
VERY v

// w's
WAS 0
WERE 7
wh :
where "":
WHICH :
whose ~:
WILL w
with )
word ~w
world _w
would wd

// y's
YOU y
young ""y
your yr
yourself yrf
yourselves yrvs";

//// Punctuation and Composition Signs
//' '
//* 99
//[*] ,7 7,
////Capital, Single ,+
////Capital, Double ,,+
//";
	}
}