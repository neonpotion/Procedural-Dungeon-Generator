using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SA
{
    public class GeneratorCell 
    {
        public int index;
        public int posX;
        public int posY;

        public int width;
        public int height;

        public bool isMainRoom = true;
        public bool isPathRoom = false;

        public bool CollidesWith(GeneratorCell cell)
        {
            bool retVal = true;

            if(cell.posX >= this.posX + this.width ||
                cell.posY >= this.posY + this.height ||
                cell.posX + cell.width <= this.posX ||
                cell.posY + cell.height <= this.posY)
            {
                retVal = false;
            }

            return retVal;
        }

        public void OffsetPosition(int offsetX, int offsetY)
        {
            posX += offsetX;
            posY += offsetY;
        }

    }
}
