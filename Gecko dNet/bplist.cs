using System;
using System.Windows.Forms;

namespace GeckoApp
{
    public partial class BPList : UserControl
    {
        public enum RegisterList
        {
            CR, XER, CTR, DSIS, DAR, SRR0, SRR1,
            r0, r1, r2, r3, r4, r5, r6, r7,
            r8, r9, r10, r11, r12, r13, r14, r15,
            r16, r17, r18, r19, r20, r21, r22, r23,
            r24, r25, r26, r27, r28, r29, r30, r31,
            LR,
            f0, f1, f2, f3, f4, f5, f6, f7,
            f8, f9, f10, f11, f12, f13, f14, f15,
            f16, f17, f18, f19, f20, f21, f22, f23,
            f24, f25, f26, f27, f28, f29, f30, f31
        }

        public Label[] longRegTextBox { get; private set; }
        public static String[] longRegNames { get; private set; } =
            new String[72] {
                "  CR"," XER"," CTR","DSIS"," DAR","SRR0","SRR1",
                "  r0","  r1","  r2","  r3","  r4","  r5","  r6","  r7",
                "  r8","  r9"," r10"," r11"," r12"," r13"," r14"," r15",
                " r16"," r17"," r18"," r19"," r20"," r21"," r22"," r23",
                " r24"," r25"," r26"," r27"," r28"," r29"," r30"," r31",
                "  LR",
                "  f0","  f1","  f2","  f3","  f4","  f5","  f6","  f7",
                "  f8","  f9"," f10"," f11"," f12"," f13"," f14"," f15",
                " f16"," f17"," f18"," f19"," f20"," f21"," f22"," f23",
                " f24"," f25"," f26"," f27"," f28"," f29"," f30"," f31"
            };
        public int[] longRegIDs { get; private set; }
        public Label[] shortRegTextBox { get; private set; }
        public String[] shortRegNames { get; private set; }

        public static int regTextToID(String reg)
        {
            for (int i = 0; i < 72; i++)
            {
                if (longRegNames[i].Contains(reg))
                {
                    return i;
                }
            }
            return 0;
        }

        public BPList()
        {
            InitializeComponent();

            longRegTextBox = new Label[72] {
                mCR,mXER,mCTR,mDSIS,mDAR,mSRR0,mSRR1,
                mR0,mR1,mR2,mR3,mR4,mR5,mR6,mR7,
                mR8,mR9,mR10,mR11,mR12,mR13,mR14,mR15,
                mR16,mR17,mR18,mR19,mR20,mR21,mR22,mR23,
                mR24,mR25,mR26,mR27,mR28,mR29,mR30,mR31,
                mLR,
                mF0,mF1,mF2,mF3,mF4,mF5,mF6,mF7,
                mF8,mF9,mF10,mF11,mF12,mF13,mF14,mF15,
                mF16,mF17,mF18,mF19,mF20,mF21,mF22,mF23,
                mF24,mF25,mF26,mF27,mF28,mF29,mF30,mF31
            };

            longRegIDs = new int[72] {
                0,1,2,3,4,5,6,39,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,
                23,24,25,26,27,28,29,30,31,32,33,34,35,36,37,38,
                40,41,42,43,44,45,46,47,48,49,50,51,52,53,54,55,56,57,58,
                59,60,61,62,63,64,65,66,67,68,69,70,71
            };

            shortRegTextBox = new Label[40] {
                mCR,mXER,mCTR,mDSIS,mDAR,mSRR0,mSRR1,
                mR0,mR1,mR2,mR3,mR4,mR5,mR6,mR7,
                mR8,mR9,mR10,mR11,mR12,mR13,mR14,mR15,
                mR16,mR17,mR18,mR19,mR20,mR21,mR22,mR23,
                mR24,mR25,mR26,mR27,mR28,mR29,mR30,mR31,
                mLR
            };

            shortRegNames = new String[40]  {
                "CR","XER","CTR","DSIS","DAR","SRR0","SRR1",
                "r0","r1","r2","r3","r4","r5","r6","r7",
                "r8","r9","r10","r11","r12","r13","r14","r15",
                "r16","r17","r18","r19","r20","r21","r22","r23",
                "r24","r25","r26","r27","r28","r29","r30","r31",
                "LR"
            };
        }

        private void BPList_Load(object sender, EventArgs e)
        {

        }
    }
}
