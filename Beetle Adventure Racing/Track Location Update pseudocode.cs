struct PntIndex
{
	ushort lnkIndex;
	ushort pntSubIndex;
}

struct Pnt
{
	Vec3 up;
	Vec3 pos;
	Vec3 fwd;
	Vec3 right;
	float trackSectionLength;
	float trackSectionRoadWidth;
}

struct CarLocationInfo
{
	PntIndex curPntIndex;
	// measured in whatever the world's units of distance are
	float progressAlongCourse;
	// this is scaled to the width of the road - i.e. the range [1.0, -1.0] corresponds to the width of the road
	float horizontalOffsetFromCenterOfTrack;
	// 0 = 0%; 65535 = 100%
	ushort amntThroughCurSection;
}

// This is called every frame for the player 
// Importantly, additionalProgress is always set to 100 on these calls!
public void UpdateCarLocationInfo(CarLocationInfo* info, float additionalProgress, Vec3* carPos)
{
	// i believe carPos is null for all AI players
	if(carPos != null)
		HandleProgressFixZones(info, carPos);
	
	// This *looks* like it's to handle some case where the race should never end, or maybe the player has
	// looped but shouldn't have their lap count increased?
	/*
    if ((((DAT_80025da4 != 0) && (0.00000000 < additionalProgress)) && (1 < unk_LAPCOUNT?)) &&
		(unk_PROGRESS_AT_START_OF_FINAL_LAP? < info->progressAlongCourse)) {
		UpdateCarLocationInfo(info,unk_PROGRESS_AT_START_OF_SECOND_LAP? - unk_PROGRESS_AT_START_OF_FINAL_LAP?,pos);
	}
	*/
	
	PntIndex curPntIndex = info.curPntIndex;
	Pnt curPnt = GetPnt(curPntIndex);
	ushort amntThroughCurSection = info.amntThroughCurSection;
	float newProgressThroughCurSection = (curPnt.trackSectionLength * (amntThroughCurSection / 65535)) + additionalProgress;
	
	while(newProgressThroughCurSection >= curPnt.trackSectionLength)
	{
		// NOTE: in the original code there may be some extra stuff going on with casting to and from a ulonglong.
		// not sure if that's real or if it's just Ghidra freaking out a bit.
		newProgressThroughCurSection -= curPnt.trackSectionLength;
		PntIndex nextPntIndex = NextPntIndex(curPntIndex);
		if(nextPntIndex.lnkIndex == -1 || nextPntIndex.pntSubIndex == -1)
		{
			// TODO
			/*
			if (DAT_803494dc != 0) {
				progressIsNegative = (float)positionWithinSection < (float)zero;
				goto LAB_80346f00;
			}
			DAT_803494dc = 1;
			break;
			*/
		}
		curPntIndex = nextPntIndex;
		curPnt = GetPnt(curPntIndex);
	}
	
	while(newProgressThroughCurSection < 0)
	{
		PntIndex prevPntIndex = PrevPntIndex(curPntIndex);
		if(prevPntIndex.lnkIndex == -1 || prevPntIndex.pntSubIndex == -1)
			break;
		curPntIndex = prevPntIndex;
		curPnt = GetPnt(curPntIndex);
		newProgressThroughCurSection += curPnt.trackSectionLength;
	}
	
	float amntThroughCurSectionFloat;
	if(carPos == null)
	{
		amntThroughCurSectionFloat = newProgressThroughCurSection / curPnt.trackSectionLength;
	}
	else
	{
		int lastPntIndexChange = 0;
		while(true): outerloop
		{
			while(true)
			{
				Vec3 posDiff = carPos - curPnt.pos;
				float distanceAlongSection = Vec3.Dot(curPnt.fwd, posDiff);
				amntThroughCurSectionFloat = distanceAlongSection / curPnt.sectionLength;
				if (amntThroughCurSectionFloat < 0)
					break;
				if (amntThroughCurSectionFloat <= 1)
					break outerloop;
				
				PntIndex nextPntIndex = NextPntIndex(curPntIndex);
				if(nextPntIndex.lnkIndex == -1 || nextPntIndex.pntSubIndex == -1) {
					amntThroughCurSectionFloat = 1;
					break outerloop;
				}
				curPntIndex = nextPntIndex;
				curPnt = GetPnt(curPntIndex);
				if(lastPntIndexChange == -1) {
					amntThroughCurSectionFloat = 0;
					break outerloop;
				}
				lastPntIndexChange = 1;
			}
			PntIndex prevPntIndex = PrevPntIndex(curPntIndex);
			amntThroughCurSectionFloat = 0;
			if(prevPntIndex.lnkIndex == -1 || prevPntIndex.pntSubIndex == -1)
				break outerloop;
			
			curPntIndex = prevPntIndex;
			curPnt = GetPnt(curPntIndex);
			
			if(lastPntIndexChange == 1) {
				amntThroughCurSectionFloat = 1;
				break outerloop;
			}
			lastPntIndexChange = -1;
		}
	}
	
	info.amntThroughCurSection = amntThroughCurSectionFloat * 65535;
	info.curPntIndex = curPntIndex;
	float pntProgress = GetProgressAtPoint(curPntIndex);
	// NOTE: in reality there's a bit of nonsense here wehere it does (float)(int)(_ * 65535) / 65535
	info.progressAlongCourse = pntProgress + (amntThroughCurSectionFloat * curPnt.trackSectionLength);
	if(carPos != null)
	{
		Vec3 posDiff = carPos - curPnt.pos;
		float f = Vec3.Dot(curPnt.right, posDiff) / curPnt.trackSectionRoadWidth;
		info.horizontalOffsetFromCenterOfTrack = f * 2;
		
	}
}

private void HandleProgressFixZones(CarLocationInfo* info, Vec3* carPos)
{
	
}
