// The data describing the track's path and checkpoints are in UVTT files.
// This data is divided into two bits - a PNTS section and a LNKS section
// The PNTS section is just a big list of objects that basically describe a segment of the
// course. I named them Pnt here although maybe "TrackSegment" would be a better name?

struct Pnt
{
	// Up vector, i.e. vector perpendicular to the ground
	Vec3 up;
	// This Pnt's "position" AKA the start position of this track segment
	Vec3 pos;
	// Forward vector, i.e. vector pointing in the direction of the track
	Vec3 fwd;
	// Right vector
	Vec3 right;
	// The length of this section of track, in world units
	float trackSectionLength;
	// The width of the road in this track segment in world units
	// I do mean "road" here - this only extends to the length of the actual road, not the entire
	// area of the course you can drive on.
	float trackSectionRoadWidth;
}

// The LNKS section is just a set of ranges defining lists of PNTs (start + length)
// For example the LNKS section for one course is
// (0, 30)
// (30, 238)
// (268, 161)
// (30, 238)
// (268, 161)
// (30, 238)
// (429, 25)

// You can see that it defines (30, 238) and (268, 161) three times, once each for each lap
// The game stores the player's current checkpoint as a LNK index plus the index of a PNT within that LNK
// For example "the 23rd point in the 4th LNK".
// If you are at the last checkpoint in a LNK, then the next checkpoint is the first checkpoint in the next LNK
// And the game calculates progress by adding up the length of all the previous track segments, taking into account the links
// (Sorry if this is confusing, I'll try to move this to an actual doc later with more thorough explanations)

struct PntIndex
{
	ushort lnkIndex;
	ushort pntSubIndex;
}

struct CarLocationInfo
{
	PntIndex curPntIndex;
	// measured in whatever the world's units of distance are
	float progressAlongCourse;
	// this is scaled to the road - i.e. the range [1.0, -1.0] corresponds to the width of the road
	float horizontalOffsetFromCenterOfTrack;
	// 0 = 0%; 65535 = 100%
	ushort amntThroughCurSection;
}

// This is called every frame for the player 
// !!!!! Importantly, additionalProgress is always set to 100 on these calls! (100 is quite large in the game's units!)
public void UpdateCarLocationInfo(CarLocationInfo* info, float additionalProgress, Vec3* carPos)
{
	// I believe carPos is null for all AI players
	if(carPos != null)
		HandleProgressFixZones(info, carPos);
	
	// TODO: This *looks* like it's to handle some case where the race should never end, or maybe the player has
	// looped but shouldn't have their lap count increased?
	/*
    if ((((DAT_80025da4 != 0) && (0.00000000 < additionalProgress)) && (1 < unk_LAPCOUNT?)) &&
		(unk_PROGRESS_AT_START_OF_FINAL_LAP? < info->progressAlongCourse)) {
		UpdateCarLocationInfo(info,unk_PROGRESS_AT_START_OF_SECOND_LAP? - unk_PROGRESS_AT_START_OF_FINAL_LAP?,pos);
	}
	*/
	
	PntIndex curPntIndex = info.curPntIndex;
	Pnt curPnt = GetPnt(curPntIndex);
	float newProgressThroughCurSection = (curPnt.trackSectionLength * (info.amntThroughCurSection / 65535)) + additionalProgress;
	
	//
	// Increment the player's checkpoint until their progress is within that checkpoint's track section
	//
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
	
	//
	// Decrement the player's checkpoint until their progress is within that checkpoint's track section
	//
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
	
	//
	// Now if the player's position is present, adjust their checkpoint based on that
	//
	if(carPos != null)
	{
		// This keeps track of what the last change was to the checkpoint index
		// We need it to avoid getting caught in an infinite loop of incrementing and decrementing back and forth
		int lastPntIndexChange = 0;
		
		while(true): outerloop
		{
			while(true) // This loop increments the player's checkpoint
			{
				Vec3 posDiff = carPos - curPnt.pos;
				float distanceAlongSection = Vec3.Dot(curPnt.fwd, posDiff);
				amntThroughCurSectionFloat = distanceAlongSection / curPnt.sectionLength;
				
				// If the player is behind the current checkpoint, stop incrementing their checkpoint
				if (amntThroughCurSectionFloat < 0)
					break;
				// If they're within the range of the current checkpoint, we're done.
				if (amntThroughCurSectionFloat <= 1)
					break outerloop;
				
				// Otherwise, increment their checkpoint
				PntIndex nextPntIndex = NextPntIndex(curPntIndex);
				if(nextPntIndex.lnkIndex == -1 || nextPntIndex.pntSubIndex == -1) {
					amntThroughCurSectionFloat = 1;
					break outerloop;
				}
				curPntIndex = nextPntIndex;
				curPnt = GetPnt(curPntIndex);
				
				// If we decremented their checkpoint before, and are now incrementing it, then stop.
				// Otherwise we get caught in an infinite loop.
				if(lastPntIndexChange == -1) {
					amntThroughCurSectionFloat = 0;
					break outerloop;
				}
				lastPntIndexChange = 1;
			}
			// Decrement the player's checkpoint
			PntIndex prevPntIndex = PrevPntIndex(curPntIndex);
			if(prevPntIndex.lnkIndex == -1 || prevPntIndex.pntSubIndex == -1)
			{
				amntThroughCurSectionFloat = 0;
				break outerloop;
			}
			curPntIndex = prevPntIndex;
			curPnt = GetPnt(curPntIndex);
			
			// If we incremented their checkpoint before, and are now decrementing it, then stop.
			// Otherwise we get caught in an infinite loop.
			if(lastPntIndexChange == 1) {
				amntThroughCurSectionFloat = 1;
				break outerloop;
			}
			lastPntIndexChange = -1;
		}
	}
	else
	{
		amntThroughCurSectionFloat = newProgressThroughCurSection / curPnt.trackSectionLength;
	}
	
	//
	// Finally, update the info struct
	//
	info.amntThroughCurSection = amntThroughCurSectionFloat * 65535;
	info.curPntIndex = curPntIndex;
	float pntProgress = GetProgressAtPoint(curPntIndex);
	// NOTE: in reality there's a bit of nonsense here wehere it does (float)((int)(amntThroughCurSectionFloat * 65535) / 65535.0)
	info.progressAlongCourse = pntProgress + (amntThroughCurSectionFloat * curPnt.trackSectionLength);
	if(carPos != null)
	{
		Vec3 posDiff = carPos - curPnt.pos;
		float f = Vec3.Dot(curPnt.right, posDiff) / curPnt.trackSectionRoadWidth;
		info.horizontalOffsetFromCenterOfTrack = f * 2;
		
	}
}

struct ProgressFixZone {
	// x center of square
	float x;
	// y center of square
	float y;
	float squareHalfSize;
	float progressMin;
	float progressMax;
	float newProgress;
}

// note: this is much more simplified pseudocode since this is not a particularly complex method
private void HandleProgressFixZones(CarLocationInfo* info, Vec3* carPos)
{
	float playerProgress = GetProgressAtPoint(GetPnt(info.curPntIndex)) 
		+ (curPnt.trackSectionLength * (info.amntThroughCurSection / 65535));
		
	for(ProgressFixZone zone : GLOBAL_LINKEDLIST_OF_PROGRESS_FIX_ZONES)
	{
		if(Math.Abs(carPos.x - zone.x) < zone.squareHalfSize
			&& Math.Abs(carPos.y - zone.y) < zone.squareHalfSize
			&& playerProgress < zone.progressMax
			&& playerProgress > zone.progressMin)
		{
			UpdateCarLocationInfo(info, zone.newProgress - playerProgress, null);
		}
	}
}
